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

		public LocalInstance(int ringLength, int ringLevel, int localPort, IProvider<byte[], ulong> hashKeyPrv) {
			Rnd = new Random();
			RingLength = ringLength;
			BackgroundTasks = new List<CancellableTask>();
			ObjectHashKeyPrv = hashKeyPrv;
			LocalNode = new LocalNodeDescriptor { Endpoint = new NodeDescriptor("127.0.0.1", localPort, ringLevel, hashKeyPrv) };
		}

		public void Join(NodeDescriptor knownNode = null) {
			if (LocalNode.State == LocalNodeState.Joined) {
				// TODO: message: already joined
			}

			var knownNodeSrv = NodeServices.GetRemoteNodeService(knownNode);
			if (knownNode != null) {
				LocalNode.Fingers[0] = new KeyValuePair<ulong, NodeDescriptor>(
							GetFingerTableKey(LocalNode.Id, 0), 
							knownNodeSrv.FindSuccessorById(LocalNode.Id)
						);	
			} else {
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
							Thread.Sleep(5000);
						}
					},
					stabilizeTaskCts.Token
				);
			BackgroundTasks.Add(new CancellableTask { Task = stabilizeTask, TokenSource = stabilizeTaskCts});

			var fixFingersTaskCts = new CancellationTokenSource();
			var fixFingersTask = Task.Factory.StartNew(
					() => {
						while (!fixFingersTaskCts.IsCancellationRequested) {
							FixFingers();
							Thread.Sleep(2000);
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

		public void Exit() {
			Leave();
			NodeServices.Clear();
		}

		public string GetRemoteServiceUrlPart() {
			return "/NodeService/" + LocalNode.Endpoint.RingLevel;
		}

		public NodeDescriptor FindResponsibleNodeByValue(IHashFunctionArgument val) {
			var objKey = ObjectHashKeyPrv.Provide(val.ToHashFunctionArgument());
			return FindSuccessorById(objKey);
		}

		public NodeDescriptor FindSuccessorById(ulong id) {
			var predecessorNode = FindPredecessorById(id);
			var predecessorNodeSrv = NodeServices.GetRemoteNodeService(predecessorNode);
			return predecessorNodeSrv.GetNodeSuccessor();
		}

		public void Stabilize() {
			var successorNodeSrv = NodeServices.GetRemoteNodeService(LocalNode.Successor);
			var succPredecessor = successorNodeSrv.GetNodePredecessor();
			if (TopologyHelper.IsInCircularInterval(succPredecessor.Id, LocalNode.Id, LocalNode.Successor.Id)) {
				LocalNode.Fingers[0] = new KeyValuePair<ulong, NodeDescriptor>(LocalNode.Fingers[0].Key, succPredecessor);
			}
			successorNodeSrv.CheckPredecessor(LocalNode.Endpoint);
		}

		public void CheckPredecessor(NodeDescriptor candidateNode) {
			if (LocalNode.Predecessor == null || TopologyHelper.IsInCircularInterval(candidateNode.Id, LocalNode.Predecessor.Id, LocalNode.Id)) {
				LocalNode.Predecessor = candidateNode;
			}
		}

		public void FixFingers() {
			var actualFingerRowIdx = GetRandomFingerTableIndex();
			LocalNode.Fingers[actualFingerRowIdx] = new KeyValuePair<ulong, NodeDescriptor>(
											GetFingerTableKey(LocalNode.Id, actualFingerRowIdx),
											FindSuccessorById(LocalNode.Fingers[actualFingerRowIdx].Key)
										);
		}

		private NodeDescriptor FindPredecessorById(ulong id) {
			if (!TopologyHelper.IsInCircularInterval(id, LocalNode.Id, LocalNode.Successor.Id, includeRight: true)) {
				var closestPrecedingFinger = FindClosestPrecedingFingerById(id);
				var closestPrecedingFingerSrv = NodeServices.GetRemoteNodeService(closestPrecedingFinger);
				return closestPrecedingFingerSrv.FindClosestPrecedingFingerById(id);
			}
			return LocalNode.Endpoint;
		}

		public NodeDescriptor FindClosestPrecedingFingerById(ulong id) {
			for (int i = (int)RingLength - 1; i >= 0; --i) {
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

		private int GetRandomFingerTableIndex() {
			return Rnd.Next(1, (int)RingLength);
		}

		private ulong GetFingerTableKey(ulong nodeId, int position) {
			var result = nodeId + (ulong)Math.Pow(2, position);
			return result > (ulong)RingLength
					? result % (ulong)Math.Pow(2, RingLength)
					: result;
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
