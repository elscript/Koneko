using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;

namespace Koneko.P2P.Chord {
	[ServiceContract]
	public interface INodeService {
		[OperationContract]
		NodeDescriptor FindSuccessorForId(ulong id);
		[OperationContract]
		NodeDescriptor GetNodePredecessor();
		[OperationContract]
		NodeDescriptor GetNodeSuccessor();
		[OperationContract]
		void FixPredecessor(NodeDescriptor candidateNode);
		[OperationContract]
		void FixSeedNode(NodeDescriptor joinedNode);
	}
}
