using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.ServiceModel;

using NReco;

using Koneko.Common.Hashing;

namespace Koneko.P2P.Chord {
	[DataContract]
	public class NodeDescriptor : IEquatable<NodeDescriptor>, IHashFunctionArgument {
		[DataMember]
		private ulong _Id;
		[DataMember]
		private int _Port;
		[DataMember]
		private string _IpAddress;
		[DataMember]
		private int _RingLevel;

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

		public int RingLevel {
			get { return _RingLevel; }
			set { _RingLevel = value; }
		}

		public NodeDescriptor(string ipAddress, int port, int ringLevel, IProvider<byte[], ulong> hashKeyPrv) {
			_IpAddress = ipAddress;
			_Port = port;
			_RingLevel = ringLevel;
			_Id = hashKeyPrv.Provide(this.ToHashFunctionArgument());
		}

		public override bool Equals(object obj) {
			return this.Equals((NodeDescriptor)obj);
		}

		public override int GetHashCode() {
			return Id.GetHashCode() ^ IpAddress.GetHashCode() ^ Port.GetHashCode() ^ RingLevel.GetHashCode();
		}

		public bool Equals(NodeDescriptor other) {
			return Id.Equals(other.Id) && Port.Equals(other.Port) && IpAddress.Equals(other.IpAddress) && RingLevel.Equals(other.RingLevel);
		}

		public byte[] ToHashFunctionArgument() {
			return Encoding.ASCII.GetBytes(IpAddress + ":" + Port + "," + RingLevel);
		}
	}
}
