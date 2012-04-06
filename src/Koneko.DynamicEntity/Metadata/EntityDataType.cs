using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public enum EntityDataTypeProperty {
		MaxLength,
		DecimalSignsBeforePoint,
		DecimalSignsAfterPoint
	}

	public class EntityDataType {
		public Type BaseType { get; set; } 
	}
}
