using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;

namespace Koneko.P2P.Chord {
	[ServiceContract]
	public interface INodeService {
		[OperationContract]
		NodeDescriptor FindSuccessorById(ulong id);
		[OperationContract]
		NodeDescriptor GetNodePredecessor();
		[OperationContract]
		NodeDescriptor GetNodeSuccessor();
		[OperationContract]
		NodeDescriptor FindClosestPrecedingFingerById(ulong id);
		[OperationContract]
		void CheckPredecessor(NodeDescriptor candidateNode);
	}
}
