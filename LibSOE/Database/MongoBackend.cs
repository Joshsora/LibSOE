using MongoDB.Bson;
using MongoDB.Driver;

namespace SOE.Database
{
    class MongoBackend : IDatabaseBackend
    {
        private readonly IMongoClient client;
        private readonly IMongoDatabase database;

        public void Connect(string host, int port)
        {
            
        }
    }
}
