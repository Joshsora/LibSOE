using System.Collections.Generic;
using SOE.Core;

namespace SOE.Database
{
    public class SOEDatabaseManager
    {
        public SOEServer Server;
        private Dictionary<string, IDatabaseBackend> Databases;

        public SOEDatabaseManager(SOEServer server)
        {
            Databases = new Dictionary<string, IDatabaseBackend>();
            Server = server;
        }

        public IDatabaseBackend GetDatabase(string name)
        {
            if (Databases.ContainsKey(name))
            {
                return Databases[name];
            }

            return null;
        }

        public void AddDatabase(string name, IDatabaseBackend database)
        {
            if (Databases.ContainsKey(name))
            {
                return;
            }

            Databases.Add(name, database);
        }
    }
}
