using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;

using NReco;
using NReco.Logging;

using Koneko.Common.Hashing;

namespace Koneko.P2P.Chord {
	[ServiceBehavior(InstanceContextMode=InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Single, IncludeExceptionDetailInFaults=true)]
	public class LocalInstance : INodeService, IDisposable {
		private static ILog Log = LogManager.GetLogger(typeof(LocalInstance));

		private readonly Random Rnd;
		private IList<CancellableTask> BackgroundTasks { get; set; }

		public IProvider<byte[], ulong> ObjectHashKeyPrv { get; set; }

		private RemoteServicesCache<INodeService> _NodeServices;
		public RemoteServicesCache<INodeService> NodeServices {
			get {
				if (_NodeServices == null) {
					_NodeServices = new RemoteServicesCache<INodeService> { 
										ServiceUrlPart = GetRemoteServiceUrlPart(),
										LocalService = this,
										LocalServiceNode = LocalNode.Endpoint
									};
				}
				return _NodeServices;
			}
		}

		public int RingLength { get; private set; }
		public LocalNodeDescriptor LocalNode { get; private set; }

		public LocalInstance(int ringLength, int ringLevel, string ipAddress, int localPort, IProvider<byte[], ulong> hashKeyPrv) {
			Rnd = new Random();
			RingLength = ringLength;
			BackgroundTasks = new List<CancellableTask>();
			ObjectHashKeyPrv = hashKeyPrv;
			LocalNode = new LocalNodeDescriptor { Endpoint = new NodeDescriptor(ipAddress, localPort, ringLevel, hashKeyPrv), RingLength = RingLength };
			// dummy init for fingers table
			InitFingerTable();
			// dummy init for successor cache
			InitSuccessorCache();
		}

		private void InitFingerTable() {
			for (int i = 0; i <= RingLength - 1; ++i) {
				LocalNode.Fingers.Add(new KeyValuePair<ulong, NodeDescriptor>(TopologyHelper.GetFingerTableKey(LocalNode.Id, i, RingLength), LocalNode.Endpoint));
			}
		}

		private void InitSuccessorCache() {
			for (int i = 0; i <= LocalNode.SuccessorCacheSize - 1; ++i) {
				LocalNode.SuccessorCache[i] = LocalNode.Endpoint;
			}
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
				Log.Write(LogEvent.Info, "Started new ring with node {0}, ring length {1}", LocalNode.Endpoint, RingLength);
			}

			// run background threads
			StartBackgroundTasks();

			LocalNode.State = NodeState.Connected;
		}

		// TODO: pass LeaveReason here (and display it)
		public void Leave() {
			Log.Write(LogEvent.Info, "Leaving the network for node {0}", LocalNode.Endpoint);
			foreach (var t in BackgroundTasks) {
				if (t.Task.Status == TaskStatus.Running) {
					t.TokenSource.Cancel();
					t.Task.Wait();
				}
			}
			InitFingerTable();
			InitSuccessorCache();
			LocalNode.InitEndpoint = null;
			LocalNode.Predecessor = null;
			NodeServices.Clear();
			LocalNode.State = NodeState.Disconnected;
			Log.Write(LogEvent.Info, "Left the network for node {0}", LocalNode.Endpoint);
		}

		private void StartBackgroundTasks() {
			var stabilizeTaskCts = new CancellationTokenSource();
			var stabilizeTask = Task.Factory.StartNew(
					() => {
						while (!stabilizeTaskCts.IsCancellationRequested) {
							Stabilize();
							Thread.Sleep(3000);
						}
					},
					stabilizeTaskCts.Token
				);
			BackgroundTasks.Add(new CancellableTask { Task = stabilizeTask, TokenSource = stabilizeTaskCts});

			var stabilizeSuccTaskCts = new CancellationTokenSource();
			var stabilizeSuccTask = Task.Factory.StartNew(
					() => {
						while (!stabilizeSuccTaskCts.IsCancellationRequested) {
							StabilizeSuccessorsCache();
							Thread.Sleep(3000);
						}
					},
					stabilizeSuccTaskCts.Token
				);
			BackgroundTasks.Add(new CancellableTask { Task = stabilizeSuccTask, TokenSource = stabilizeSuccTaskCts});

			var fixFingersTaskCts = new CancellationTokenSource();
			var fixFingersTask = Task.Factory.StartNew(
					() => {
						var fingerRowIdxToFix = 0;
						while (!fixFingersTaskCts.IsCancellationRequested) {
							FixFingers(fingerRowIdxToFix);
							fingerRowIdxToFix = fingerRowIdxToFix == RingLength - 1 ? 0 : fingerRowIdxToFix + 1;
							Thread.Sleep(3000);
						}
					},
					fixFingersTaskCts.Token
				);
			BackgroundTasks.Add(new CancellableTask { Task = fixFingersTask, TokenSource = fixFingersTaskCts});
		}

		public string GetRemoteServiceUrlPart() {
			return "/NodeService/" + LocalNode.Endpoint.RingLevel;
		}

		public NodeDescriptor FindResponsibleNodeForValue(IHashFunctionArgument val) {
			var objKey = ObjectHashKeyPrv.Provide(val.ToHashFunctionArgument());
			return FindSuccessorForId(objKey);
		}

		private void Stabilize() {
			Log.Write(LogEvent.Debug, "Calling stabilize for node {0}", LocalNode.Endpoint);
			var successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);

			NodeDescriptor succPredecessor = null;
			try {
				succPredecessor = successorNodeSrv.Service.GetNodePredecessor();
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilization because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					SetSuccessorFromCache();
					return;
				} else {
					throw;
				}
			}

			if (succPredecessor != null) {
				// include right?
				if (TopologyHelper.IsInCircularInterval(succPredecessor.Id, LocalNode.Id, LocalNode.Successor.Id)) {
					Log.Write(LogEvent.Debug, "Successor for node {0} changed from {1} to {2} during stabilization", LocalNode.Endpoint, LocalNode.Successor, succPredecessor);
					LocalNode.Successor = succPredecessor;

					// our successor can be a different node now, refreshing it's service
					successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);
				}
			}

			try {
				successorNodeSrv.Service.FixPredecessor(LocalNode.Endpoint);
				Log.Write(LogEvent.Debug, "Finished stabilizing for node {0}", LocalNode.Endpoint);
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilization because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					SetSuccessorFromCache();
					return;
				} else {
					throw;
				}
			}
		}

		private void StabilizeSuccessorsCache() {
			Log.Write(LogEvent.Debug, "Calling stabilize successor cache for node {0}", LocalNode.Endpoint);

			var successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);
			NodeDescriptor successorNodeSuccessor = null;
			NodeDescriptor[] successorNodeSuccessorCache = null;

			try {
				successorNodeSuccessor = successorNodeSrv.Service.GetNodeSuccessor();
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilize successor cache because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					SetSuccessorFromCache();
					return;
				} else {
					throw;
				}
			}

			try {
				successorNodeSuccessorCache = successorNodeSrv.Service.GetNodeSuccessorCache();
			} catch (Exception ex) {
				if (successorNodeSrv.IsUnavailable) {
					Log.Write(
						LogEvent.Warn, 
						"Cannot perform stabilize successor cache because service for node {0} successor ({1}) is unavailable, trying to reassign first available cached successor as successor. Error details: \r\n {2}", 
						LocalNode.Endpoint, 
						LocalNode.Successor,
						ex.ToString()
					);
					SetSuccessorFromCache();
					return;
				} else {
					throw;
				}
			}
			
			if (successorNodeSuccessor != null || successorNodeSuccessorCache != null) {
				for (var i = 0; i < LocalNode.SuccessorCacheSize; ++i) {
					if (i == 0) {
						if (successorNodeSuccessor != null) {
							LocalNode.SuccessorCache[i] = successorNodeSuccessor;
						}
					} else {
						if (successorNodeSuccessorCache != null && successorNodeSuccessorCache[i-1] != null) {
							LocalNode.SuccessorCache[i] = successorNodeSuccessorCache[i-1];
						}
					}
				}
				Log.Write(LogEvent.Debug, "Finished stabilizing successor cache for node {0}", LocalNode.Endpoint);
			}
		}

		public void FixPredecessor(NodeDescriptor candidateNode) {
			Log.Write(LogEvent.Debug, "Calling fix predecessor for node {0} with candidate {1}", LocalNode.Endpoint, candidateNode);
			if (LocalNode.Predecessor == null || LocalNode.Predecessor.Equals(LocalNode.Endpoint) || TopologyHelper.IsInCircularInterval(candidateNode.Id, LocalNode.Predecessor.Id, LocalNode.Id)) {
				LocalNode.Predecessor = candidateNode;
				Log.Write(LogEvent.Debug, "Fixed predecessor for node {0} with candidate {1}", LocalNode.Endpoint, candidateNode);
			}
		}

		// special method that checks if the seed node succ/pred are ok
		public void FixSeedNode(NodeDescriptor joinedNode) {
			if (LocalNode.Successor.Equals(LocalNode.Endpoint)) {
				 // its not correct because there is at least one more node in the network (which called this method)
				LocalNode.Successor = joinedNode;
			}
		}

		private void FixFingers(int? fingerRowIdx = null) {
			var actualFingerRowIdx = fingerRowIdx.HasValue 
										? (fingerRowIdx.Value > RingLength - 1 
												? 0
												: fingerRowIdx.Value 
											)
										: GetRandomFingerTableIndex();
			Log.Write(LogEvent.Debug, "Calling fix fingers for node {0}, finger row position {1}", LocalNode.Endpoint, actualFingerRowIdx);
			
			var successorForFinger = FindSuccessorForId(LocalNode.Fingers[actualFingerRowIdx].Key);

			LocalNode.Fingers[actualFingerRowIdx] = new KeyValuePair<ulong, NodeDescriptor>(TopologyHelper.GetFingerTableKey(LocalNode.Id, actualFingerRowIdx, RingLength), successorForFinger);
			Log.Write(LogEvent.Debug, "Finished fixing fingers for node {0}, finger row position {1}", LocalNode.Endpoint, actualFingerRowIdx);
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

		private void SetSuccessorFromCache() {
			foreach (var succ in LocalNode.SuccessorCache) {
				if (!succ.Equals(LocalNode.Endpoint)) {
					LocalNode.Endpoint = succ;
					return;
				}
			}
			// there's no cached nodes adequate for becoming temp successor, we need to try rejoining
			Rejoin();
		}

		// TODO: pass RejoinReason here (and display it)
		private void Rejoin() { 
			var localInitEndpoint = LocalNode.InitEndpoint;
			if (localInitEndpoint != null) {
				Log.Write(LogEvent.Info, "Trying to rejoin for node {0}, bootstrapper node {1}", LocalNode.Endpoint, localInitEndpoint);
				// leaving the network but preserve the capability to rejoin
				Leave();

				Join(localInitEndpoint);
			}

			// means that the cached initial node is unaccessiable
			if (localInitEndpoint == null || LocalNode.State != NodeState.Connected) {
				Log.Write(LogEvent.Warn, "Not possible to rejoin for node {0} because the bootstrapper node is empty or not accessible, leaving the network", LocalNode.Endpoint);

				// leave the network completely (manual rejoin is possible)
				Leave();	
			}
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

		private int GetRandomFingerTableIndex() {
			return Rnd.Next(1, (int)RingLength);
		}

		public void Dispose() {
			if (NodeServices is IDisposable) {
				((IDisposable)NodeServices).Dispose();
			}
		}

		private class CancellableTask {
			public Task Task { get; set; }
			public CancellationTokenSource TokenSource { get; set; }
		}
	}
}
