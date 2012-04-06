using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public class DynamicEntity : IDynamicEntity {
		public IDynamicField Fields { get; set; }
	}
}
