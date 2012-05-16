using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Koneko.P2P.Chord {
	[DataContract]
	public class NodeDescriptor : IEquatable<NodeDescriptor> {
		[DataMember]
		private ulong _Id;
		[DataMember]
		private int _Port;
		[DataMember]
		private string _IpAddress;

		public ulong Id {
			get { return _Id; }
			private set { _Id = value; }
		}

		public string IpAddress {
			get { return _IpAddress; }
			set { _IpAddress = value; }
		}

		public int Port {
			get { return _Port; }
			set { _Port = value; }
		}

		public NodeDescriptor(string ipAddress, int port) {
			_IpAddress = ipAddress;
			_Port = port;
			_Id = GetNodeId();
		}

		public override bool Equals(object obj) {
			return this.Equals((NodeDescriptor)obj);
		}

		public override int GetHashCode() {
			return Id.GetHashCode() ^ IpAddress.GetHashCode() ^ Port.GetHashCode();
		}

		public bool Equals(NodeDescriptor other) {
			return Id.Equals(other.Id) && Port.Equals(other.Port) && IpAddress.Equals(IpAddress);
		}

		private ulong GetNodeId() {
			var prv = new SHA1CryptoServiceProvider();
			var hashInput = Encoding.ASCII.GetBytes(IpAddress + ":" + Port);
			return BitConverter.ToUInt64(prv.ComputeHash(hashInput), 0);
		}
	}
}
