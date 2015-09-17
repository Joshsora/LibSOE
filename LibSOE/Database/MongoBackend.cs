using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace SOE.Database
{
    public class MongoBackend : IDatabaseBackend
    {
        private IMongoClient client;
        private IMongoDatabase database;

        public void Connect(string host, int port)
        {
            // Connect!
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
    }
}
