using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Koneko.DynamicEntity.Metadata;
using Koneko.DynamicEntity.Storage;

using DynamicEntityImpl = Koneko.DynamicEntity.Metadata.DynamicEntity;
using MongoQueryBuilder = MongoDB.Driver.Builders.Query;
using MongoUpdateBuilder = MongoDB.Driver.Builders.Update;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Driver.Builders;

namespace Koneko.DynamicEntity.MongoDB {
	public class DynamicEntityMetadataStorage : IDynamicEntityMetadataStorage {
		public string ConnectionString { get; set; }

		private MongoUrlBuilder ConnectionStringWrapper {
			get { return new MongoUrlBuilder(ConnectionString); }
		}

		private MongoServer _Server;
		private MongoServer Server {
			get {
				if (_Server == null) {
					_Server = MongoServer.Create(ConnectionString);
				}
				return _Server;
			}
		}

		private MongoDatabase _Database;
		private MongoDatabase Database {
			get {
				if (_Database == null) {
					_Database = Server.GetDatabase(Convert.ToString(ConnectionStringWrapper.DatabaseName));
				}
				return _Database;
			}
		}

		public string[] EntityNames {
			get { 
				return Database.GetCollectionNames().ToArray(); 
			}
		}

		public StorageOperationResult Create(IDynamicEntity entity) {
			MongoCollection<BsonDocument> descriptors = GetCollection();
			var insertData = DynamicEntityToBsonAdapter.ToBsonDocument(entity);
			descriptors.Insert(insertData);
			return new StorageOperationResult { AffectedRecords = 1 };
		}

		public IDynamicEntity Load(string entityName) {
			MongoCollection<BsonDocument> descriptors = GetCollection();
			//var qDescriptors = descriptors.AsQueryable<BsonDocument>();
			//var result = qDescriptors.FirstOrDefault(x => Convert.ToString(x["name"]) == sourcename);
			var result = descriptors.FindOne(MongoQueryBuilder.EQ("name", entityName));
			return result != null ? DynamicEntityToBsonAdapter.FromBsonDocument(result) : null;
		}

		public StorageOperationResult Update(IDynamicEntity newEntity) {
			// TODO: move diff logic to some diff handler when it will be needed to capture history or sth
			MongoCollection<BsonDocument> descriptors = GetCollection();
			//var qDescriptors = descriptors.AsQueryable<BsonDocument>();
			//var oldEntityDoc = qDescriptors.FirstOrDefault(x => Convert.ToString(x["name"]) == newEntity.Name);
			var oldEntityDoc = descriptors.FindOne(MongoQueryBuilder.EQ("name", newEntity.Name));
			if (oldEntityDoc == null) {
				return new StorageOperationResult { 
							AffectedRecords = 0, 
							Error = new Exception(String.Format("There's no dynamic with the name {0} present in the storage", newEntity.Name)) 
				};
			}
			descriptors.Update(
					MongoQueryBuilder.EQ("name", newEntity.Name),
					MongoUpdateBuilder.Set("fields", DynamicEntityToBsonAdapter.ToBsonDocument(newEntity)["fields"])
			);
			return new StorageOperationResult { AffectedRecords = 1 };
		}

		public StorageOperationResult Delete(string entityName) {
			MongoCollection<BsonDocument> descriptors = GetCollection();
			var res = descriptors.Remove(MongoQueryBuilder.EQ("name", new BsonString(entityName)), RemoveFlags.None, SafeMode.True);
			return new StorageOperationResult { AffectedRecords = (int)res.DocumentsAffected };
		}

		private MongoCollection<BsonDocument> GetCollection() {
			// TODO: the sourcename should be configurable
			return Database.GetCollection("dynamic_entity_descriptors");
		}

		static class DynamicEntityToBsonAdapter {
			public static BsonDocument ToBsonDocument(IDynamicEntity entity) {
				var fields = new BsonArray();
				foreach (var f in entity.Fields) {
					var props = f.Properties != null && f.Properties.Any()
										? new BsonArray(f.Properties.Select(p => 
													new BsonDocument{ 
														{ "name", p.Key },
														{ "value", BsonValue.Create(p.Value) }
													}
											))
										: null;
					var fDoc = new BsonDocument {
						{ "name", f.Name },
						{ "datatype", f.DataType.FullName }
					};
					if (f.DefaultValue != null) {
						fDoc["default_value"] = BsonValue.Create(f.DefaultValue); 
					}
					if (props != null) {
						fDoc["properties"] = props;
					}
					fields.Add(fDoc);
				}
				var result = new BsonDocument {
					{ "name", entity.Name },
					{ "fields", new BsonArray(fields) }
				};
				return result;
			}

			public static IDynamicEntity FromBsonDocument(BsonDocument doc) {
				var fields = new List<IDynamicField>();
				foreach (var f in doc["fields"].AsBsonArray) {
					var fDoc = f.AsBsonDocument;
					// TODO: here should be factory method that will return correct field class, as for now always create SimpleDynamicField instance
					var field = new SimpleDynamicField {
						Name = fDoc["name"].AsString,
						DataType = Type.GetType(fDoc["datatype"].AsString)
					};
					if (fDoc.Contains("default_value")) {
						field.DefaultValue = fDoc["default_value"].RawValue; // ?
					}
					if (fDoc.Contains("properties")) {
						field.Properties = fDoc["properties"].AsBsonArray.ToDictionary(k => k.AsBsonDocument["name"].AsString, v => v.AsBsonDocument["value"].RawValue);
					}
					fields.Add(field);
				}

				// TODO: here should be factory method that will return correct entity class, as for now always create DynamicEntity instance
				var result = new DynamicEntityImpl { 
					Name = doc["name"].AsString,
					Fields = fields.ToArray()
				};
				return result;
			}
		}	
	}
}
