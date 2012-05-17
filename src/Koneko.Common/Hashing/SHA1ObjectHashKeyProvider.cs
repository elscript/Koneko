using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using NReco;

namespace Koneko.Common.Hashing {
	public class SHA1ObjectHashKeyProvider : IProvider<byte[], ulong> {
		public ulong? Modulo { get; set; }

		public ulong Provide(byte[] hashArgument) {
			var prv = new SHA1CryptoServiceProvider();
			var result = BitConverter.ToUInt64(prv.ComputeHash(hashArgument), 0);
			return Modulo.HasValue && result > Modulo.Value ? result % Modulo.Value : result;; 
		}
	}
}
