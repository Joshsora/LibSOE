using SOE.Core;
using System.IO;
namespace SOEDaemon
{
    class Program
    {
        private static Daemon daemon;

        static void Main(string[] args)
        {
            // TEMPORARY: Create a sample config
            if (!File.Exists("daemon.cfg"))
            {
                new Daemon()
                {
                    {"server1", new SOEServer(20032, "protocol1")},
                    {"server2", new SOEServer(20034, "protocol2")},
                    {"server3", new SOEServer(20036, "protocol3")}
                }.SaveConfig();
            }

            daemon = new Daemon();
            daemon.LoadConfig();  // Load config
        }
    }
}
