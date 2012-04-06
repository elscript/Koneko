using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public interface IDynamicField {
		string Name { get; set; }

		EntityDataType DataType { get; set; }

		object DefaultValue { get; set; }
		T GetTypedDefaultValue<T>();

		bool IsRequired { get; set; }

		IDictionary<EntityDataTypeProperty, object> Properties { get; set; }
	}
}