﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.P2P.Storage {
	public interface IStorage<StoredT> {
		void Put(StoredT val);
		StoredT Get(IStorageQuery query);
	}
}
