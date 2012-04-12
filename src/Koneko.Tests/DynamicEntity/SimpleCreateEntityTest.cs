using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Koneko.DynamicEntity.Metadata;
using KonekoDynamicEntity = Koneko.DynamicEntity.Metadata.DynamicEntity;

namespace Koneko.Tests.DynamicEntity {
	[TestFixture]
	[Category("Koneko.DynamicEntity")]
	public class EntityMetadataTest {
		[Test]
		public void TestEntityCreation() {
			var entity = new KonekoDynamicEntity {
				Name = "financial_accounts", 
				Fields = new[] {
					new SimpleDynamicField { 
							Name = "name",
							DataType = typeof(string), 
							IsRequired = true, 
							DefaultValue = "n/a",
 							Properties = new Dictionary<string, object> {
								{ EntityDataTypeProperty.MaxLength, 100 }
							}
					},

					new SimpleDynamicField { 
							Name = "amount",
							DataType = typeof(decimal), 
							IsRequired = true, 
							DefaultValue = 1000,
 							Properties = new Dictionary<string, object> {
								{ EntityDataTypeProperty.DecimalSignsBeforePoint, 10 },
								{ EntityDataTypeProperty.DecimalSignsAfterPoint, 2 }
							}
					}
				}
			};

			Console.WriteLine(
				String.Format(
					"Default value of dynamicentity:{0}:{1} equals {2}", 
					entity.Name, 
					"amount", 
					entity["amount"].GetTypedDefaultValue<decimal>()
			));

			Console.WriteLine(
				String.Format(
					"Property {0} of dynamicentity:{1}:{2} equals {3}", 
					EntityDataTypeProperty.DecimalSignsAfterPoint, 
					entity.Name, 
					"amount", 
					entity["amount"].GetTypedPropertyValue<short>(EntityDataTypeProperty.DecimalSignsAfterPoint)
			));
		}
	}
}
