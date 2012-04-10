using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public class DynamicEntity : IDynamicEntity {
		public string Name { get; set; }
		public IDynamicField this[string name] {
			get { 
				return Fields.SingleOrDefault(f => f.Name == name );
			}
		}
		public IDynamicField[] Fields { get; set; }
	}
}
