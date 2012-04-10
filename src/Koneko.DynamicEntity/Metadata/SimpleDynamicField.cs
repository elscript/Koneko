using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NReco.Converting;

namespace Koneko.DynamicEntity.Metadata {
	public class SimpleDynamicField : IDynamicField {
		public string Name { get; set; }
		public EntityDataType DataType { get; set; }

		public object DefaultValue { get; set; }

		public T GetTypedDefaultValue<T>() {
			try {
				return (T)Convert.ChangeType(DefaultValue, typeof(T));
			} catch (Exception ex) {
				throw new Exception(String.Format("Cannot convert default value of dynamic field {0} from type {1} to type {2}", Name, DefaultValue.GetType(), typeof(T)), ex);
			}
		}

		public bool IsRequired { get; set; }

		public IDictionary<string, object> Properties { get; set; }

		public T GetTypedPropertyValue<T>(string propertyName) {
			if (!Properties.ContainsKey(propertyName)) {
				throw new Exception(String.Format("There is no such property in the {0} field", Name));
			}
			var val = Properties[propertyName];
			try {
				return (T)Convert.ChangeType(val, typeof(T));
			} catch (Exception ex) {
				throw new Exception(String.Format("Cannot convert {0} property value of dynamic field {1} from type {2} to type {3}", propertyName, Name, val.GetType(), typeof(T)), ex);
			}
		}
	}
}
