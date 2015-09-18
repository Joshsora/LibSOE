using Newtonsoft.Json;
using SOE.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOEDaemon
{
    public class Daemon : Dictionary<string, SOEServer>
    {
        // TODO: Internal Dictionary?

        public new void Add(string name, SOEServer server)
        {
            base.Add(name, server);
            server.Run();
        }

        public new void Remove(string name)
        {
            base.Remove(name);
            // TODO: Stop server thread gracefully
        }

        public void LoadConfig(string path = "daemon.cfg")
        {
            if (File.Exists(path))
            {
                Daemon loaded = JsonConvert.DeserializeObject<Daemon>(File.ReadAllText("daemon.cfg"));
                foreach (KeyValuePair<string, SOEServer> server in loaded)
                    if(!ContainsKey(server.Key))
                        Add(server.Key, server.Value);
            }
            else Console.WriteLine("No daemon configuration file found.");
        }

        public void SaveConfig(string path = "daemon.cfg")
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
