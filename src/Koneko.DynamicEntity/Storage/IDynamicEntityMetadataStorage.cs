using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.DynamicEntity.Metadata;

namespace Koneko.DynamicEntity.Storage {
	public class StorageOperationResult {
		public int AffectedRecords { get; set; }
		public Exception Error { get; set; }
	}

	public interface IDynamicEntityMetadataStorage {
		string[] EntityNames { get; }
		IDynamicEntity Load(string entityName);
		StorageOperationResult Create(IDynamicEntity entity);
		StorageOperationResult Update(IDynamicEntity entity);
		StorageOperationResult Delete(string entityName);
	}
}
