using System;
using System.Collections.Generic;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using log4net.Repository;

namespace SOE.Core
{
    public class SOELogger
    {
        // Components
        public SOEServer Server;

        // Repo
        public ILoggerRepository Repo;

        // Settings
        public Dictionary<string, dynamic> Configuration = new Dictionary<string, dynamic>
        {
            // Basic information
            {"Filename", "SOEServer.log"},
            {"LogPatern", "<%date> :%logger: [%level] %message%newline"},

            // Types of logging
            {"WantConsoleLogging", true},
            {"WantFileLogging", true},

            {"WantLibraryLogging", true},
            {"WantApplicationLogging", false},

            // Severities
            {"WantInfo", true},
            {"WantDebug", true},
            {"WantError", true},
            {"WantWarning", true},

            // Prettiness
            {"WantColors", false}
        };

        public SOELogger(SOEServer server)
        {
            Server = server;
        }

        public void StartLogging()
        {

        }

        public void Configure(Dictionary<string, dynamic> configuration)
        {
            foreach (var configVariable in configuration)
            {
                if (!Configuration.ContainsKey(configVariable.Key))
                {
                    // Bad configuration variable
                    Console.WriteLine("Invalid configuration variable '{0}' for SOELogger instance. Ignoring.", configVariable.Key);
                    continue;
                }

                // Set this variable
                Configuration[configVariable.Key] = configVariable.Value;
            }
        }
    }
}
