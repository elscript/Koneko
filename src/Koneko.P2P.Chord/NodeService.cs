using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.P2P.Chord {
	public class NodeService : INodeService {
		public LocalInstance LocalInstance { get; set; }

		public NodeDescriptor FindSuccessorById(ulong id) {
			return LocalInstance.FindSuccessorById(id);
		}

		public NodeDescriptor GetNodePredecessor() {
			return LocalInstance.LocalNode.Predecessor;
		}

		public NodeDescriptor GetNodeSuccessor() {
			return LocalInstance.LocalNode.Successor;
		}

		public void CheckPredecessor(NodeDescriptor candidateNode) {
			LocalInstance.CheckPredecessor(candidateNode);
		}

		public NodeDescriptor FindClosestPrecedingFingerById(ulong id) {
			return LocalInstance.FindClosestPrecedingFingerById(id);
		}
	}
}
