using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;

using NUnit.Framework;

using Koneko.DynamicEntity.Metadata;
using Koneko.DynamicEntity.MongoDB;
using KonekoDynamicEntity = Koneko.DynamicEntity.Metadata.DynamicEntity;

namespace Koneko.Tests.DynamicEntity {
	[TestFixture]
	[Category("Koneko.DynamicEntity")]
	public class MongoDbStorageTest {
		[Test]
		public void StoreAndRetrieve() {
			//StartMongoDb();
			var storage = new DynamicEntityMetadataStorage {
				ConnectionString = "mongodb://localhost/test_dynamic_entity" 
			};

			var loadedEntity0 = storage.Load("financial_accounts");
			Assert.IsNull(loadedEntity0);

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
			
			Assert.AreEqual(1000, entity["amount"].GetTypedDefaultValue<decimal>());

			var res = storage.Create(entity);

			Assert.AreEqual(1, res.AffectedRecords);

			var loadedEntity = storage.Load("financial_accounts");

			Assert.AreEqual(100, loadedEntity["name"].GetTypedPropertyValue<int>(EntityDataTypeProperty.MaxLength));
			Assert.AreEqual(1000, loadedEntity["amount"].GetTypedDefaultValue<decimal>());

			loadedEntity["amount"].DefaultValue = 500;

			Assert.AreEqual(500, loadedEntity["amount"].GetTypedDefaultValue<decimal>());

			res = storage.Update(loadedEntity);
			Assert.AreEqual(1, res.AffectedRecords);

			var loadedEntity2 = storage.Load("financial_accounts");
			Assert.AreEqual(500, loadedEntity2["amount"].GetTypedDefaultValue<decimal>());

			res = storage.Delete("financial_accounts");

			Assert.AreEqual(1, res.AffectedRecords);

			var loadedEntity3 = storage.Load("financial_accounts");
			Assert.IsNull(loadedEntity3);

			//StopMongoDb();
		}

		// install mongodb as a service: mongod --install
		// name it "mongodb": mongod --serviceName mongodb (do not forget to specify --dbpath if needed)
		// specify service params: --dbpath <path to data> --directoryperdb
		private void StartMongoDb() {
			var srv = new ServiceController("MongoDB");
			if (srv.Status == ServiceControllerStatus.Stopped) {
				srv.Start();
				srv.WaitForStatus(ServiceControllerStatus.Running);
			}
		}

		private void StopMongoDb() {
			var srv = new ServiceController("MongoDB");
			srv.Stop();
			srv.WaitForStatus(ServiceControllerStatus.Stopped);
		}
	}
}
