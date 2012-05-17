using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;

using Koneko.Common.Hashing;

namespace Koneko.Common.Storage {
	[ServiceContract]
	public interface IStorageService {
		[OperationContract]
		void Put(IHashFunctionArgument val);
		[OperationContract]
		object Get(IStorageQuery q);
	}
}
