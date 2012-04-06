using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.DynamicEntity.Metadata;

namespace Koneko.DynamicEntity.Storage {
	public interface IDynamicEntityStorage {
		IDynamicEntity Load(string sourcename);
	}
}
