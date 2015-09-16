using MongoDB.Bson;
using MongoDB.Driver;

namespace SOE.Database
{
    class MongoBackend : IDatabaseBackend
    {
        private IMongoClient client;
        private readonly IMongoDatabase database;

        public void Connect(string username, string password, string host, int port)
        {
            // Connect!
            string connectionString = string.Format("mongo://{0}:{1}@{2}:{3}/soe_db", username, password, host, port);
            client = new MongoClient(connectionString);
        }
    }
}
