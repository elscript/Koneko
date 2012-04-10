using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.DynamicEntity.Metadata;

namespace Koneko.DynamicEntity.Storage {
	public interface IDynamicEntityMetadataStorage {
		IDynamicEntity Load(string sourcename);
		void Update(IDynamicEntity entity);
	}
}
