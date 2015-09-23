using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace SOE.Database
{
    public class MongoBackend : IDatabaseBackend
    {
        private IMongoClient Client;
        private IMongoDatabase Database;

        private readonly List<string> Storables;
        private readonly Dictionary<string, List<FieldInfo>> Uniques;

        private bool SettingUp;

        public MongoBackend()
        {
            Storables = new List<string>();
            Uniques = new Dictionary<string, List<FieldInfo>>();
            SettingUp = false;
        }

        public void Connect(string host, int port)
        {
            string connectionString = string.Format("mongodb://{0}:{1}", host, port);
            Client = new MongoClient(connectionString);

            Database = Client.GetDatabase("soe_db");
        }

        public async Task<bool> Setup(Type[] storables)
        {
            // If any functions are called while we're still setting up, hold them until we're finished
            SettingUp = true;

            // Used for logging
            int missingCollections = 0;

            Log("Setting-up database...");
            foreach (Type storable in storables)
            {
                if (Storables.Contains(storable.FullName))
                {
                    continue;
                }

                Storables.Add(storable.FullName);
                string collectionName = storable.Name;

                bool collectionExists = await CollectionExistsAsync(collectionName);
                if (!collectionExists)
                {
                    // Create the collection
                    await Database.CreateCollectionAsync(collectionName);
                    Log("Created missing collection, '{0}'", collectionName);
                    missingCollections++;

                    // Get the fields
                    Dictionary<FieldInfo, uint> uniques = new Dictionary<FieldInfo, uint>();
                    var fields = storable.GetFields();

                    // Get the collection
                    var collection = Database.GetCollection<BsonDocument>(collectionName);

                    // Go through each field in this object and check for uniques
                    foreach (var field in fields)
                    {
                        // Get the attributes for this field
                        var attributes = field.GetCustomAttributes(typeof (UniqueDBField));

                        // Are there are attributes?
                        if (attributes.Any())
                        {
                            // Create the index
                            BsonDocument index = new BsonDocument();
                            index.Add("fields." + field.Name, 1);

                            // Add the index
                            await collection.Indexes.CreateOneAsync(index, new CreateIndexOptions()
                            {
                                Unique = true
                            });

                            // Add this attribute name to uniques with the next value
                            uniques.Add(field, 0);
                        }

                        // Only do this once
                        break;
                    }

                    // Do we have uniques?
                    if (uniques.Any())
                    {
                        // Create the controller
                        BsonDocument controller = new BsonDocument();
                        BsonDocument uniquesDoc = new BsonDocument();
                        controller.Add("_t", "CollectionController");

                        foreach (var unique in uniques)
                        {
                            uniquesDoc.Add(unique.Key.Name, unique.Value);
                        }

                        controller.Add("uniques", uniquesDoc);

                        // Insert the controller
                        await collection.InsertOneAsync(controller);
                    }

                    // Add it to our Uniques
                    Uniques.Add(collectionName, uniques.Keys.ToList());
                }
                else
                {
                    // Get the fields
                    List<FieldInfo> uniques = new List<FieldInfo>();
                    var fields = storable.GetFields();

                    // Get the collection
                    var collection = Database.GetCollection<BsonDocument>(collectionName);

                    // Go through each field in this object and check for uniques
                    foreach (var field in fields)
                    {
                        // Get the attributes for this field
                        var attributes = field.GetCustomAttributes(typeof(UniqueDBField));

                        // Are there are attributes?
                        if (attributes.Any())
                        {
                            // Create the index
                            BsonDocument index = new BsonDocument();
                            index.Add("fields." + field.Name, 1);

                            // Add the index
                            await collection.Indexes.CreateOneAsync(index, new CreateIndexOptions()
                            {
                                Unique = true
                            });

                            // Add this attribute name to uniques with the next value
                            uniques.Add(field);
                        }

                        // Only do this once
                        break;
                    }

                    // Add it to our Uniques
                    Uniques.Add(collectionName, uniques);
                }
            }

            // Final log
            Log("Created {0} missing collections", missingCollections);
            Log("Setup finished!");

            // Finished!
            SettingUp = false;
            return true;
        }

        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var collections = await Database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
            return (await collections.ToListAsync()).Any();
        }

        public async Task<TResult> RunCommand<TResult>(string command)
        {
            return await Database.RunCommandAsync<TResult>(command);
        }

        public async Task<TResult> Query<TResult>(Dictionary<string, dynamic> filter) where TResult : new()
        {
            // We need to be fully setup before we do anything..
            while (SettingUp)
            {
                // Wait until we're done
                await Task.Yield();
            }

            // Is this a storable type?
            if (!IsStorable(typeof(TResult)))
            {
                // Don't handle this type
                return new TResult();
            }

            // Get the type name for the filter
            string className = typeof(TResult).Name;

            // Get the collection
            var collection = Database.GetCollection<BsonDocument>(className);

            // Prepare the filter
            BsonDocument completeFilter = new BsonDocument();
            completeFilter.Add("_t", className);
            if (filter.Any())
            {
                Dictionary<string, dynamic> newFilter = filter.ToDictionary(
                    x => "fields." + x.Key,
                    x => x.Value
                );

                completeFilter.Add(newFilter);
            }

            // Get our cursor, and go through the documents..
            using (var cursor = await collection.FindAsync(completeFilter))
            {
                // Get the batch
                await cursor.MoveNextAsync();
                var batch = cursor.Current;

                // If there aren't any..
                if (!batch.Any())
                {
                    // Return a new TResult
                    return new TResult();
                }

                // Get the document
                foreach (var document in batch)
                {
                    // Deserialize the object!
                    return BsonSerializer.Deserialize<TResult>(document["fields"].ToBsonDocument());
                }
            }

            // Object didn't exist..
            return new TResult();
        }

        public async Task<bool> Insert(dynamic obj)
        {
            // We need to be fully setup before we do anything..
            while (SettingUp)
            {
                // Wait until we're done
                await Task.Yield();
            }

            // Is this a storable type?
            if (!IsStorable(obj.GetType()))
            {
                // Don't handle this type
                return false;
            }

            // Get the collection
            IMongoCollection<BsonDocument> collection = Database.GetCollection<BsonDocument>(obj.GetType().Name);

            // Check for uniques
            List<FieldInfo> uniques = Uniques[obj.GetType().Name];
            if (uniques.Any())
            {
                BsonDocument uniquesDoc = new BsonDocument();

                // Next, get the collection controller
                BsonDocument filter = new BsonDocument();
                filter.Add("_t", "CollectionController");

                using (var cursor = await collection.FindAsync(filter))
                {
                    // Get the batch
                    await cursor.MoveNextAsync();
                    var batch = cursor.Current;

                    // If there aren't any..
                    if (!batch.Any())
                    {
                        // We don't care :')
                        Log("[ERROR] Collection Controller not found for type '{0}'!", obj.GetType().Name);
                        return false;
                    }

                    // Get the document
                    foreach (BsonDocument document in batch)
                    {
                        uniquesDoc = document["uniques"].ToBsonDocument();
                        break;
                    }
                }

                // Update this object on our side and on the DB
                BsonDocument update = new BsonDocument();
                BsonDocument uniquesUpdate = new BsonDocument();

                foreach (var unique in uniques)
                {
                    if (uniquesDoc.Contains(unique.Name))
                    {
                        // Set the value for the object
                        unique.SetValue(obj, (uint)uniquesDoc.GetValue(unique.Name).AsInt64);

                        // Get the new value for the DB
                        uint newValue = (uint)uniquesDoc.GetValue(unique.Name).AsInt64 + 1;
                        uniquesUpdate.Add("uniques." + unique.Name, newValue);
                    }
                }
                update.Add("$set", uniquesUpdate);

                // Update the collection manager on the DB
                await collection.UpdateOneAsync(filter, update);
            }

            // Serialize the object!
            BsonDocument serialized = new BsonDocument();
            serialized.Add("_t", obj.GetType().Name);

            // Prepare the objects fields!
            BsonDocument fields = new BsonDocument();
            using (BsonWriter writer = new BsonDocumentWriter(fields))
            {
                BsonSerializer.Serialize(writer, obj.GetType(), obj);
            }
            fields.Remove("_t");
            serialized.Add("fields", fields);

            // Insert the serialized object
            await collection.InsertOneAsync(serialized);
            return true;
        }

        public async Task<bool> Update<T>(Dictionary<string, dynamic> filter, Dictionary<string, dynamic> update)
        {
            // We need to be fully setup before we do anything..
            while (SettingUp)
            {
                // Wait until we're done
                await Task.Yield();
            }

            // Is this a storable type?
            if (!IsStorable(typeof (T)))
            {
                // Don't handle this type
                return false;
            }

            // Get the type name for the filter
            string className = typeof (T).Name;

            // Prepare the filter
            BsonDocument completeFilter = new BsonDocument();
            completeFilter.Add("_t", className);
            if (filter.Any())
            {
                Dictionary<string, dynamic> newFilter = filter.ToDictionary(
                    x => "fields." + x.Key,
                    x => x.Value
                );

                completeFilter.Add(newFilter);
            }

            // Serialize the object!
            BsonDocument serialized = new BsonDocument();
            BsonDocument fields = new BsonDocument();

            // Add the objects fields!
            if (update.Any())
            {
                Dictionary<string, dynamic> newUpdate = update.ToDictionary(
                    x => "fields." + x.Key,
                    x => x.Value
                );

                // Will this update change a Unique field?
                foreach (FieldInfo unique in Uniques[className])
                {
                    // Do not update unique fields!
                    if (newUpdate.ContainsKey(unique.Name))
                    {
                        newUpdate.Remove(unique.Name);
                    }
                }

                // Add to the fields for this update..
                fields.Add(newUpdate);
            }
            serialized.Add("$set", fields);

            // Get the collection
            var collection = Database.GetCollection<BsonDocument>(className);

            // Update!
            await collection.UpdateOneAsync(completeFilter, serialized);
            return true;
        }

        public bool IsStorable(Type type)
        {
            return Storables.Contains(type.FullName);
        }

        public void Log(string message, params object[] args)
        {
            Console.WriteLine(":MongoBackend: " + message, args);
        }
    }
}
