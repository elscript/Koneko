using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace Koneko.P2P.Chord {
	[ServiceContract]
	public interface INodeService {
		[OperationContract]
		Node FindSuccessorById(long nodeId, long initialNodeId);
		[OperationContract]
		Node GetNodeSuccessor(
	}
}
