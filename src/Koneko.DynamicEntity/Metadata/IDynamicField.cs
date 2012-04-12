using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Koneko.DynamicEntity.Metadata {
	public interface IDynamicField {
		string Name { get; set; }

		Type DataType { get; set; }

		object DefaultValue { get; set; }
		T GetTypedDefaultValue<T>();

		bool IsRequired { get; set; }

		IDictionary<string, object> Properties { get; set; }
		T GetTypedPropertyValue<T>(string name);
	}
}