using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
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
                await cursor.MoveNextAsync();
                var batch = cursor.Current;
                if (!batch.Any())
                {
                    return new TResult();
                }

                // Get the document
                foreach (var document in batch)
                {
                    if (!document.Contains("fields"))
                    {
                        // This is not an object..
                        continue;
                    }

                    // Deserialize the object!
                    return BsonSerializer.Deserialize<TResult>(document["fields"].ToBsonDocument());
                }

                // Didn't exist..
                return new TResult();
            }
        }

        /*
        public async Task<uint> Insert(string collection, MongoStorable storable)
        {
            
        }

        public async Task<bool> Delete(string collection, uint id)
        {
            
        }
         */
    }
}
