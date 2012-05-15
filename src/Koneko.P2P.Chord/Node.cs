using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.P2P.Chord {
	public enum NodeState {
		Disconnected, Joined
	}
	
	public abstract class Node {
		public long Id { get; private set; }
		public IList<KeyValuePair<long, Node>> Fingers { get; private set; }
		public NodeState State { get; private set; }
		public Node Predecessor { get; private set; }
		public Network NodesNetwork { get; private set; }

		public Node Successor {
			get { 
				if (Fingers.Any()) {
					return Fingers[0].Value;
				}
				return null;
			}
		}

		public Node(Network network) {
			State = NodeState.Disconnected;
			Fingers = new List<KeyValuePair<long, Node>>();
			NodesNetwork = network;
		}

		public void Join(Node knownNode = null) {
			if (knownNode == null) { // create new network
				for (int i = 0; i <= NodesNetwork.Length - 1; ++i) {
					Fingers[i] = new KeyValuePair<long, Node>(NodesNetwork.GetFingerTableKey(Id, i), this);
				}
				Predecessor = this;
			} else {
				Fingers[0] = new KeyValuePair<long, Node>(
								NodesNetwork.GetFingerTableKey(Id, 0), 
								NodesNetwork.FindSuccessorById(Id, knownNode)
							);
			}
			State = NodeState.Joined;
		}

		public void Stabilize() {
			var succPredecessor = Successor.Predecessor;
			if (NodesNetwork.IsInCircularInterval(succPredecessor.Id, Id, Successor.Id)) {
				Fingers[0] = new KeyValuePair<long, Node>(Fingers[0].Key, succPredecessor);
			}
			Successor.CheckPredecessor(this);
		}

		public void CheckPredecessor(Node predecessorCandidateNode) {
			if (Predecessor == null || NodesNetwork.IsInCircularInterval(predecessorCandidateNode.Id, Predecessor.Id, Id)) {
				Predecessor = predecessorCandidateNode;
			}
		}

		public void FixFingers(int? fingerRowIdx) {
			var actualFingerRowIdx = fingerRowIdx.HasValue ? fingerRowIdx.Value : NodesNetwork.GetRandomFingerTableIndex();
			Fingers[actualFingerRowIdx] = new KeyValuePair<long, Node>(
											NodesNetwork.GetFingerTableKey(Id, actualFingerRowIdx),
											NodesNetwork.FindSuccessorById(Fingers[actualFingerRowIdx].Key, this)
										);
		}

		public void Leave() {
			State = NodeState.Disconnected;
			// TODO
		}
	}
}
