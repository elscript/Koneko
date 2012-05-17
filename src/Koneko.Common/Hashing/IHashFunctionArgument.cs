using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.Common.Hashing {
	public interface IHashFunctionArgument {
		byte[] ToHashFunctionArgument();
	}
}
