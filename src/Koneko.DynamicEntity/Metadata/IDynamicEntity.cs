using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public interface IDynamicEntity {
		IDynamicField Fields { get; set; }
	}
}
