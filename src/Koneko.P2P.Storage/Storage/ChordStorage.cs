using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ChordInstance = Koneko.P2P.Chord.LocalInstance;

namespace Koneko.P2P.Storage.Chord {
	public class ChordStorage<StoredT> : IStorage<StoredT> {
		public ChordInstance Chord { get; set; }

		public void Put(StoredT val) {
			throw new NotImplementedException();
		}

		public StoredT Get(IStorageQuery query) {
			throw new NotImplementedException();
		}
	}
}
