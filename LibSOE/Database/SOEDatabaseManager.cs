using System.Collections.Generic;

namespace SOE.Database
{
    public class SOEDatabaseManager
    {
        private Dictionary<string, IDatabaseBackend> Databases;

        public SOEDatabaseManager()
        {
            Databases = new Dictionary<string, IDatabaseBackend>();
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
