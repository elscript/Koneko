using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;

using NReco;

using Koneko.Common.Hashing;

namespace Koneko.P2P.Chord {
	[ServiceBehavior(InstanceContextMode=InstanceContextMode.Single)]
	public class LocalInstance : INodeService, IDisposable {
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
			LocalNode = new LocalNodeDescriptor { Endpoint = new NodeDescriptor(ipAddress, localPort, ringLevel, hashKeyPrv) };
		}

		public void Join(NodeDescriptor knownNode = null) {
			if (LocalNode.State == LocalNodeState.Joined) {
				// TODO: message: already joined
			}

			if (knownNode != null) {
				if (knownNode.Equals(LocalNode.Endpoint)) {
					 // TODO: message: the known node cannot be the same as the local node
				}
				var knownNodeSrv = NodeServices.GetRemoteNodeService(knownNode);
				LocalNode.Fingers[0] = new KeyValuePair<ulong, NodeDescriptor>(
							GetFingerTableKey(LocalNode.Id, 0), 
							knownNodeSrv.FindSuccessorForId(LocalNode.Id)
						);	
			} else {
				// start new ring
				for (int i = 0; i <= RingLength - 1; ++i) {
					LocalNode.Fingers[i] = new KeyValuePair<ulong, NodeDescriptor>(GetFingerTableKey(LocalNode.Id, i), LocalNode.Endpoint);
				}
				LocalNode.Predecessor = LocalNode.Endpoint;
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
							Thread.Sleep(1000);
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
							++fingerRowIdxToFix;
							Thread.Sleep(1000);
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
			// if there are no other nodes in the network - no need to stabilize anything
			if (!LocalNode.Successor.Equals(LocalNode.Endpoint)) {
				var successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);
				var succPredecessor = successorNodeSrv.GetNodePredecessor();
				if (TopologyHelper.IsInCircularInterval(succPredecessor.Id, LocalNode.Id, LocalNode.Successor.Id)) {
					LocalNode.Fingers[0] = new KeyValuePair<ulong, NodeDescriptor>(LocalNode.Fingers[0].Key, succPredecessor);
				}
				successorNodeSrv.FixPredecessor(LocalNode.Endpoint);
			}	
		}

		public void FixPredecessor(NodeDescriptor candidateNode) {
			if (LocalNode.Predecessor == null || TopologyHelper.IsInCircularInterval(candidateNode.Id, LocalNode.Predecessor.Id, LocalNode.Id)) {
				LocalNode.Predecessor = candidateNode;
			}
		}

		private void FixFingers(int? fingerRowIdx = null) {
			var actualFingerRowIdx = fingerRowIdx.HasValue 
										? (fingerRowIdx.Value > RingLength - 1 
												? 0
												: fingerRowIdx.Value 
											)
										: GetRandomFingerTableIndex();
			LocalNode.Fingers[actualFingerRowIdx] = new KeyValuePair<ulong, NodeDescriptor>(
											GetFingerTableKey(LocalNode.Id, actualFingerRowIdx),
											FindSuccessorForId(LocalNode.Fingers[actualFingerRowIdx].Key)
										);
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

		private ulong GetFingerTableKey(ulong nodeId, int position) {
			var result = nodeId + (ulong)Math.Pow(2, position);
			var modulo = (ulong)Math.Pow(2, RingLength);
			return result > modulo ? result % modulo : result;
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
