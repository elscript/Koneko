using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.Common.Hashing {
	public class HashableString : IHashFunctionArgument {
		public string Value { get; set; }

		public byte[] ToHashFunctionArgument() {
			return Encoding.ASCII.GetBytes(Value);
		}

		public static implicit operator string(HashableString s) {
			return s.Value;
		}

		public static implicit operator HashableString(string s) {
			return new HashableString { Value = s };
		}
	}
}
