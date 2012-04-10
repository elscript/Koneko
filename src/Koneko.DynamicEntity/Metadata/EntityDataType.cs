using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NReco.Converting;

namespace Koneko.DynamicEntity.Metadata {
	public static class EntityDataTypeProperty {
		public static readonly string MaxLength = "maxlength";
		public static readonly string DecimalSignsBeforePoint = "decimal_signs_before_point";
		public static readonly string DecimalSignsAfterPoint = "decimal_signs_after_point";

		//public static IDictionary<string, Type> PropertyTypes;

		/*static EntityDataTypeProperty() {
			PropertyTypes = new Dictionary<string, Type> {
				{ EntityDataTypeProperty.MaxLength, typeof(int) },
				{ EntityDataTypeProperty.DecimalSignsBeforePoint, typeof(short) },
				{ EntityDataTypeProperty.DecimalSignsAfterPoint, typeof(short) }
			};
		}*/
	}

	public class EntityDataType {
		public Type BaseType { get; set; } 
	}
}
