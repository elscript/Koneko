using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public interface IDynamicEntity {
		string Name { get; set; }
		IDynamicField this[string name] { get; }
		IDynamicField[] Fields { get; set; }
	}
}
