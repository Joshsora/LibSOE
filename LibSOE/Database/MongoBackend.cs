using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace SOE.Database
{
    public class MongoBackend : IDatabaseBackend
    {
        private IMongoClient client;
        private IMongoDatabase database;

        public void Connect(string host, int port)
        {
            string connectionString = string.Format("mongodb://{0}:{1}", host, port);
            client = new MongoClient(connectionString);

            database = client.GetDatabase("soe_db");
        }

        public async void Setup(string[] collections)
        {
            foreach (string collectionName in collections)
            {
                bool collectionExists = await CollectionExistsAsync(collectionName);
                if (!collectionExists)
                {
                    await database.CreateCollectionAsync(collectionName);
                }
            }
        }

        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var collections = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
            return (await collections.ToListAsync()).Any();
        }

        public async Task<TResult> RunCommand<TResult>(string collection, string command)
        {
            return await database.RunCommandAsync<TResult>(command);
        }

        public async Task<TResult> Query<TResult>(string collectionName, Dictionary<string, dynamic> filter) where TResult : new()
        {
            // Get the collection
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Get the type name for the filter
            string className = typeof (TResult).Name;

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

        public async void Insert(string collectionName, dynamic obj) 
        {
            // Serialize the object!
            BsonDocument serialized = new BsonDocument();
            serialized.Add("_t", obj.GetType().Name);

            // Add the objects fields!
            BsonDocument fields = new BsonDocument();
            using (BsonWriter writer = new BsonDocumentWriter(fields))
            {
                BsonSerializer.Serialize(writer, obj.GetType(), obj);
            }
            fields.Remove("_t");
            serialized.Add("fields", fields);

            // Get the collection
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Insert the serialized object
            await collection.InsertOneAsync(serialized);
        }

        public async void Update<TResult>(string collectionName, Dictionary<string, dynamic> filter, Dictionary<string, dynamic> update)
        {
            // Get the type name for the filter
            string className = typeof (TResult).Name;

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

                fields.Add(newUpdate);
            }
            serialized.Add("$set", fields);

            // Get the collection
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Update!
            await collection.UpdateOneAsync(completeFilter, serialized);
        }
    }
}
