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
										ServiceUrlPart = GetRemoteServiceUrlPart() 
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
			for (int i = 0; i <= RingLength - 1; ++i) {
				LocalNode.Fingers.Add(new KeyValuePair<ulong, NodeDescriptor>(TopologyHelper.GetFingerTableKey(LocalNode.Id, i, RingLength), LocalNode.Endpoint));
			}
		}

		public void Join(NodeDescriptor knownNode = null) {
			if (LocalNode.State == LocalNodeState.Joined) {
				Log.Write(LogEvent.Warn, "Node {0} cannot join the network because it is aleady marked as joined", LocalNode.Endpoint);
				return;
			}

			if (knownNode != null) {
				if (knownNode.Equals(LocalNode.Endpoint)) {
					Log.Write(LogEvent.Warn, "Bootstrapper node {0} cannot be the same as the local node {1}", knownNode, LocalNode.Endpoint);
					return;
				} else {
					Log.Write(LogEvent.Info, "Joining the ring with node {0}, bootstrapper node {1}, ring length {2}", LocalNode.Endpoint, knownNode, RingLength);
					var knownNodeSrv = NodeServices.GetRemoteNodeService(knownNode);
					var nodeSuccessor = knownNodeSrv.FindSuccessorForId(LocalNode.Id);
					// set my own successor & predecessor
					LocalNode.Successor = nodeSuccessor;
					LocalNode.Predecessor = nodeSuccessor;
					// try fixing the successor/predecessor of the seed node
					knownNodeSrv.FixSeedNode(LocalNode.Endpoint);
					Log.Write(LogEvent.Info, "Joined the ring with node {0}, bootstrapper node {1}, ring length {2}", LocalNode.Endpoint, knownNode, RingLength);
				}
			} else {
				// start new ring
				Log.Write(LogEvent.Info, "Starting new ring with node {0}, ring length {1}", LocalNode.Endpoint, RingLength);
				LocalNode.Predecessor = LocalNode.Endpoint;
				Log.Write(LogEvent.Info, "Started new ring with node {0}, ring length {1}", LocalNode.Endpoint, RingLength);
			}

			// run background threads
			StartBackgroundTasks();

			LocalNode.State = LocalNodeState.Joined;
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

		public void Leave() {
			if (LocalNode.State == LocalNodeState.Joined) {
				LocalNode.State = LocalNodeState.Disconnected;

				foreach (var t in BackgroundTasks) {
					if (t.Task.Status == TaskStatus.Running) {
						t.TokenSource.Cancel();
						t.Task.Wait();
					}
				}
			} else {
				// TODO: message: has not joined yet
			}
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
			var succPredecessor = successorNodeSrv.GetNodePredecessor();
			if (succPredecessor != null) {
				if (TopologyHelper.IsInCircularInterval(succPredecessor.Id, LocalNode.Id, LocalNode.Successor.Id)) {
					LocalNode.Successor = succPredecessor;
				}
				successorNodeSrv.FixPredecessor(LocalNode.Endpoint);
				Log.Write(LogEvent.Debug, "Finished stabilizing for node {0}", LocalNode.Endpoint);
			} else {
				Log.Write(LogEvent.Debug, "Cannot perform stabilizing for node {0}, the successor's ({1}) predecessor node is null", LocalNode.Endpoint, LocalNode.Successor);
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
			if (LocalNode.Predecessor == null || LocalNode.Predecessor.Equals(LocalNode.Endpoint)) {
				 // its not correct because there is at least one more node in the network (which called this method)
				LocalNode.Predecessor = joinedNode;
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

			var closestPredecessorFromFingers = FindClosestPredecessorFromFingers(id);
			if (closestPredecessorFromFingers.Equals(LocalNode.Endpoint)) {
				return LocalNode.Endpoint;
			}

			var closestPredecessorFromFingersNodeSrv = NodeServices.GetRemoteNodeService(closestPredecessorFromFingers);
			return closestPredecessorFromFingersNodeSrv.FindSuccessorForId(id);
		}

		private NodeDescriptor FindClosestPredecessorFromFingers(ulong id) {
			var rightVal = LocalNode.Id;
			for (int i = RingLength - 1; i >= 0; --i) {
				if (TopologyHelper.IsInCircularInterval(id, LocalNode.Fingers[i].Key, rightVal)) {
					return LocalNode.Fingers[i].Value;
				}
				rightVal = LocalNode.Fingers[i].Key;
			}
			return LocalNode.Endpoint;
		}

		public NodeDescriptor GetNodePredecessor() {
			return LocalNode.Predecessor;
		}

		public NodeDescriptor GetNodeSuccessor() {
			return LocalNode.Successor;
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
