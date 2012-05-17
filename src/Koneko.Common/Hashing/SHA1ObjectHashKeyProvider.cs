using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using NReco;

namespace Koneko.Common.Hashing {
	public class SHA1ObjectHashKeyProvider : IProvider<byte[], ulong> {
		public ulong Provide(byte[] hashArgument) {
			var prv = new SHA1CryptoServiceProvider();
			return BitConverter.ToUInt64(prv.ComputeHash(hashArgument), 0);
		}
	}
}
