using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.Common {
	public interface IStorage<StoredT> {
		void Put(StoredT val);
		StoredT Get(IStorageQuery query);
	}
}
