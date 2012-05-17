using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.Common.Hashing;

namespace Koneko.Common.Storage {
	public interface IStorageQuery {
		IHashFunctionArgument GetArgument();
	}
}
