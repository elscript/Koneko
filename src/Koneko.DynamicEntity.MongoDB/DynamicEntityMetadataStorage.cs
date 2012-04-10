using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.DynamicEntity.Metadata;
using Koneko.DynamicEntity.Storage;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Koneko.DynamicEntity.MongoDB {
	public class DynamicEntityMetadataStorage : IDynamicEntityMetadataStorage {
		public IDynamicEntity Load(string sourcename) {
			throw new NotImplementedException();
		}

		public void Update(IDynamicEntity entity) {
			throw new NotImplementedException();
		}
	}
}
