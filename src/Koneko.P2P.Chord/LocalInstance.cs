using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;

using NReco;
using NReco.Logging;

using Koneko.Common;
using Koneko.Common.Hashing;

namespace Koneko.P2P.Chord {
	[ServiceBehavior(InstanceContextMode=InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Single, IncludeExceptionDetailInFaults=true)]
	public class LocalInstance : INodeService, IDisposable {
		private static ILog Log = LogManager.GetLogger(typeof(LocalInstance));

		public IProvider<byte[], ulong> ObjectHashKeyPrv { get; set; }
		public LocalInstanceMaintenanceService MaintenanceService { get; set; }
		public CommunicationManager<INodeService> CommunicationManager { get; set; }
		public RemoteServicesCache<INodeService> NodeServices { get; set; }

		public int RingLength { get; private set; }
		public LocalNodeDescriptor LocalNode { get; private set; }

		private LocalInstanceTask InnerTask { get; set; }

		public ulong Id {
			get { return LocalNode.Id; }
		}

		public LocalInstance(int ringLength, int ringLevel, int localPort, IProvider<byte[], ulong> hashKeyPrv) {
			RingLength = ringLength;
			ObjectHashKeyPrv = hashKeyPrv;
			LocalNode = new LocalNodeDescriptor(new NodeDescriptor(NetworkHelper.GetLocalIpAddress(), localPort, ringLevel, hashKeyPrv), ringLength);

			CommunicationManager = new CommunicationManager<INodeService> {
										LocalService = this,
										LocalServiceNode = LocalNode.Endpoint
									};

			NodeServices = new RemoteServicesCache<INodeService> { 
									ServiceUrlPart = CommunicationManager.ServiceUrlPart,
									LocalService = this,
									LocalServiceNode = LocalNode.Endpoint
								};

			MaintenanceService = new LocalInstanceMaintenanceService(this) { NodeServices = NodeServices };
		}

		public void Join(NodeDescriptor knownNode = null) {
			if (LocalNode.State == NodeState.Connected) {
				Log.Write(LogEvent.Warn, "Node {0} cannot join the network because it is aleady marked as joined", LocalNode.Endpoint);
				return;
			}

			if (knownNode != null) {
				LocalNode.InitEndpoint = knownNode;

				if (knownNode.Equals(LocalNode.Endpoint)) {
					Log.Write(LogEvent.Warn, "Bootstrapper node {0} cannot be the same as the local node {1}", knownNode, LocalNode.Endpoint);
					return;
				} else {
					Log.Write(LogEvent.Info, "Joining the ring with node {0}, bootstrapper node {1}, ring length {2}", LocalNode.Endpoint, knownNode, RingLength);

					try {
						CommunicationManager.StartCommunication();
					} catch (Exception ex) {
						Log.Write(LogEvent.Error, "Cannot start local service for node {0}. Error details: \r\n {1}", LocalNode.Endpoint, ex.ToString());
					}

					var knownNodeSrv = NodeServices.GetRemoteNodeService(knownNode);

					try {
						var nodeSuccessor = knownNodeSrv.Service.FindSuccessorForId(LocalNode.Id);
						// set my own successor
						LocalNode.Successor = nodeSuccessor;
					} catch (Exception ex) {
						if (knownNodeSrv.IsUnavailable) {
							Log.Write(LogEvent.Warn, "Service for node {0} is unavailable, please retry or rejoin with another seed. Error details: \r\n {1}", knownNode, ex.ToString());
							Leave();
							return;
						} else {
							throw;
						}
					}

					// try fixing the successor of the seed node
					try {
						knownNodeSrv.Service.FixSeedNode(LocalNode.Endpoint);

						// joined successfully
						Log.Write(LogEvent.Info, "Joined the ring with node {0}, bootstrapper node {1}, ring length {2}", LocalNode.Endpoint, knownNode, RingLength);
					} catch (Exception ex) {
						if (knownNodeSrv.IsUnavailable) {
							Log.Write(LogEvent.Warn, "Service for node {0} is unavailable, please retry or rejoin with another seed. Error details: \r\n {1}", knownNode, ex.ToString());
							Leave();
							return;
						} else {
							throw;
						}
					}
				}
			} else {
				// start new ring
				Log.Write(LogEvent.Info, "Starting new ring with node {0}, ring length {1}", LocalNode.Endpoint, RingLength);

				try {
					CommunicationManager.StartCommunication();
				} catch (Exception ex) {
					Log.Write(LogEvent.Error, "Cannot start local service for node {0}. Error details: \r\n {1}", LocalNode.Endpoint, ex.ToString());
				}

				Log.Write(LogEvent.Info, "Started new ring with node {0}, ring length {1}", LocalNode.Endpoint, RingLength);
			}

			LocalNode.State = NodeState.Connected;

			// if all is ok, start instance thread
			StartInnerTask();
		}

		private void StartInnerTask() {
			// if we are rejoining, then the task is already running, so we only need to resume it
			if (InnerTask.ActualTask.Status == TaskStatus.Running) {
				// allow to resume maintenance
				MaintenanceService.Status = MaintenanceStatus.WaitingForStart;
			} else {
				var factory = new TaskFactory(TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent, TaskContinuationOptions.None);

				InnerTask = new LocalInstanceTask();
				InnerTask.ActualTask 
					= factory.StartNew(
						() => {
							var taskFinished = false;
							while (!taskFinished) {
								// run background threads
								if (MaintenanceService.Status == MaintenanceStatus.WaitingForStart) {
									MaintenanceService.Start();
								}

								// wait until event is received
								InnerTask.WaitingForEventWaitHandle.Wait();

								if (InnerTask.CurrentEvent == LocalInstanceEvent.RejoinRequested) {
									if (LocalNode.InitEndpoint == null) {
										Log.Write(LogEvent.Warn, "Not possible to rejoin for node {0} because the bootstrapper node is empty or not accessible, leaving the network", LocalNode.Endpoint);
										// cannot rejoin - simply request leaving
										InnerTask.SetCurrentEvent(LocalInstanceEvent.LeaveRequested);
									} else {
										// try to rejoin using cached bootstraper node
										var localInitEndpoint = LocalNode.InitEndpoint;
										Log.Write(LogEvent.Info, "Trying to rejoin for node {0}, bootstrapper node {1}", LocalNode.Endpoint, localInitEndpoint);

										Leave();
										Join(localInitEndpoint);

										if (LocalNode.State != NodeState.Connected) {
											// still couldn't join the network for some reason - request leaving
											InnerTask.SetCurrentEvent(LocalInstanceEvent.LeaveRequested);
										} else {
											InnerTask.MarkCurrentEventAsProcessed();
										}
									}
								} else if (InnerTask.CurrentEvent == LocalInstanceEvent.LeaveRequested || InnerTask.CurrentEvent == LocalInstanceEvent.ExitRequested) {
									Leave();
									// there may be some events pending -> discard them
									InnerTask.ProcessingEventWaitHandle.Set();
									taskFinished = true;
								}
							}
						}
					);
			}
		}

		// method for the instance thread signaling
		public void SignalEvent(LocalInstanceEvent e) {
			// wait until current event is being processed
			InnerTask.ProcessingEventWaitHandle.Wait();
			// proceed
			InnerTask.SetCurrentEvent(e);
		}

		// TODO: pass LeaveReason here (and display it)
		public void Leave() {
			Log.Write(LogEvent.Info, "Leaving the network for node {0}", LocalNode.Endpoint);

			// stop stabilization threads
			MaintenanceService.Stop();
			
			// stop network communication server
			CommunicationManager.StopCommunication();

			// cleanup
			NodeServices.Clear();
			LocalNode.Reset();

			// mark as disconnected
			LocalNode.State = NodeState.Disconnected;

			Log.Write(LogEvent.Info, "Left the network for node {0}", LocalNode.Endpoint);
		}

		public NodeDescriptor FindResponsibleNodeForValue(IHashFunctionArgument val) {
			var objKey = ObjectHashKeyPrv.Provide(val.ToHashFunctionArgument());
			return FindSuccessorForId(objKey);
		}

		// special method that checks if the seed node succ/pred are ok
		public void FixSeedNode(NodeDescriptor joinedNode) {
			if (LocalNode.Successor.Equals(LocalNode.Endpoint)) {
				 // its not correct because there is at least one more node in the network (which called this method)
				LocalNode.Successor = joinedNode;
			}
		}

		public NodeDescriptor FindSuccessorForId(ulong id) {
			// check if my successor is actually the successor of this id
			if (TopologyHelper.IsInCircularInterval(id, LocalNode.Id, LocalNode.Successor.Id, includeRight: true)) {
				return LocalNode.Successor;
			}

			// check if me is actually the successor of this id
			if (LocalNode.Predecessor != null && TopologyHelper.IsInCircularInterval(id, LocalNode.Predecessor.Id, LocalNode.Id, includeRight: true)) {
				return LocalNode.Endpoint;
			}

			var pred = FindPredecessorForId(id);

			var predSrv = NodeServices.GetRemoteNodeService(pred);
			try {
				return predSrv.Service.GetNodeSuccessor();
			} catch (Exception ex) {
				if (predSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Node {0} cannot call GetNodeSuccessor for node {1} because the service is unaccessible, returning local node instead. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						pred,
						ex.ToString()
					);

					// TODO: correct?
					return LocalNode.Endpoint;
				} else {
					throw;
				}
			}
		}

		public void FixPredecessor(NodeDescriptor candidateNode) {
			Log.Write(LogEvent.Debug, "Calling fix predecessor for node {0} with candidate {1}", LocalNode.Endpoint, candidateNode);
			if (LocalNode.Predecessor == null || LocalNode.Predecessor.Equals(LocalNode.Endpoint) || TopologyHelper.IsInCircularInterval(candidateNode.Id, LocalNode.Predecessor.Id, LocalNode.Id)) {
				LocalNode.Predecessor = candidateNode;
				Log.Write(LogEvent.Debug, "Fixed predecessor for node {0} with candidate {1}", LocalNode.Endpoint, candidateNode);
			}
		}

		private NodeDescriptor FindPredecessorForId(ulong id) {
			var result = FindClosestPrecedingFinger(id);
			
			// stop recursion
			if (result.Equals(LocalNode.Endpoint)) {
				return LocalNode.Endpoint;
			}

			if (TopologyHelper.IsInCircularInterval(id, LocalNode.Id, LocalNode.Successor.Id, includeRight: true)) {
				return result;
			} else {
				var resultNodeSrv = NodeServices.GetRemoteNodeService(result);

				try {
					return resultNodeSrv.Service.FindSuccessorForId(id);
				} catch (Exception ex) {
					if (resultNodeSrv.IsUnavailable) {
						Log.Write(
							LogEvent.Warn, 
							"Node {0} cannot propagate FindSuccessorForId for node {1} because the service is unaccessible, returning local node instead. Error details: \r\n {2}", 
							LocalNode.Endpoint, 
							resultNodeSrv,
							ex.ToString()
						);

						// TODO: correct?
						return LocalNode.Endpoint;
					} else {
						throw;
					}
				}
			}
		}

		private NodeDescriptor FindClosestPrecedingFinger(ulong id) {
			for (int i = RingLength - 1; i >= 0; --i) {
				if (TopologyHelper.IsInCircularInterval(LocalNode.Fingers[i].Value.Id, LocalNode.Id, id)) {
					return LocalNode.Fingers[i].Value;
				}
			}
			return LocalNode.Endpoint;
		}

		public NodeDescriptor GetNodePredecessor() {
			return LocalNode.Predecessor;
		}

		public NodeDescriptor GetNodeSuccessor() {
			return LocalNode.Successor;
		}

		public NodeDescriptor[] GetNodeSuccessorCache() {
			return LocalNode.SuccessorCache;
		}

		public void Dispose() {
			if (CommunicationManager is IDisposable) {
				((IDisposable)CommunicationManager).Dispose();
			}
			if (NodeServices is IDisposable) {
				((IDisposable)NodeServices).Dispose();
			}
		}

		public void Ping() {
			// do nothing
		}
	}

	public class LocalInstanceTask {
		public Task ActualTask { get; set; }
		public ManualResetEventSlim WaitingForEventWaitHandle { get; set; }
		public ManualResetEventSlim ProcessingEventWaitHandle { get; set; }
		public LocalInstanceEvent CurrentEvent { get; set; }

		// TODO: in future it can be implemented as queue of events instead of 'receive event - process - wait for another' model

		public LocalInstanceTask() {
			WaitingForEventWaitHandle = new ManualResetEventSlim();
			// this handle is signaled initially, because we need to be able to receive events
			ProcessingEventWaitHandle = new ManualResetEventSlim(true);
			// no events initially
			CurrentEvent = LocalInstanceEvent.None;
		}

		public void SetCurrentEvent(LocalInstanceEvent e) {
			CurrentEvent = e;
			// start processing events
			WaitingForEventWaitHandle.Set();
			// do not receive event until processed
			ProcessingEventWaitHandle.Reset();
		}

		public void MarkCurrentEventAsProcessed() {
			CurrentEvent = LocalInstanceEvent.None;
			// start waiting for new event
			WaitingForEventWaitHandle.Reset();
			// allow new events
			ProcessingEventWaitHandle.Set();
		}
	}

	public enum LocalInstanceEvent {
		None, LeaveRequested, ExitRequested, RejoinRequested
	}
}
