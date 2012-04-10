using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.DynamicEntity.Metadata;

namespace Koneko.DynamicEntity.Storage {
	public class MongoDbDynamicEntityStorage : IDynamicEntityMetadataStorage {
		public IDynamicEntity Load(string sourcename) {
			throw new NotImplementedException();
		}

		public void Update(IDynamicEntity entity) {
			throw new NotImplementedException();
		}
	}
}
