using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;
using System.Runtime.Serialization;

namespace Koneko.P2P.Chord {
	[ServiceContract]
	public interface INodeService {
		[OperationContract]
		FindNodeForIdResult FindSuccessorForId(FindNodeForIdArg id);
		[OperationContract]
		NodeDescriptor GetNodePredecessor();
		[OperationContract]
		NodeDescriptor GetNodeSuccessor();
		[OperationContract]
		void FixPredecessor(NodeDescriptor candidateNode);
		[OperationContract]
		void FixSeedNode(NodeDescriptor joinedNode);
		[OperationContract]
		NodeDescriptor[] GetNodeSuccessorCache();
		[OperationContract]
		void Ping();
	}

	[DataContract]
	public class FindNodeForIdArg {
		[DataMember]
		public ulong Id { get; private set; }
		[DataMember]
		public bool ReturnSuccessorAsResult { get; private set; }

		public FindNodeForIdArg(ulong id, bool returnSuccessorAsResult) {
			Id = id;
			ReturnSuccessorAsResult = returnSuccessorAsResult;
		}

		public static implicit operator ulong(FindNodeForIdArg arg) {
			return arg.Id;
		}

		public static implicit operator FindNodeForIdArg(ulong id) {
			return new FindNodeForIdArg(id, true);
		}

		public override string ToString() {
			return Id.ToString();
		}
	}

	[DataContract]
	public class FindNodeForIdResult {
		[DataMember]
		public NodeDescriptor Node { get; private set; }
		[DataMember]
		public bool ReturnSuccessorAsResult { get; private set; }

		public FindNodeForIdResult(NodeDescriptor result, bool returnSuccessorAsResult) {
			Node = result;
			ReturnSuccessorAsResult = returnSuccessorAsResult;
		}

		public static implicit operator NodeDescriptor(FindNodeForIdResult res) {
			return res.Node;
		}

		public static implicit operator FindNodeForIdResult(NodeDescriptor node) {
			return new FindNodeForIdResult(node, false);
		}

		public override string ToString() {
			return Node.ToString();
		}
	}
}
