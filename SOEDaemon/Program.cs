using System;
using System.Collections.Generic;
using CommandLine;
using SOE.Core;

namespace SOEDaemon
{
    class Program
    {
        private static List<SOEServer> Servers; 

        static void Main(string[] args)
        {
            DaemonOptions options = new DaemonOptions();

            // Successful parse?
            if (Parser.Default.ParseArguments(args, options))
            {
                // Are we verbose?
                if (options.Verbose)
                {
                    // Log verbosely
                    Log("Using configuration: {0}", options.ConfigFile);
                }

                // Configure!
                Configure(options.ConfigFile);
            }
        }

        static void Configure(string configFile)
        {

        }

        static void Log(string message, params object[] args)
        {
            Console.WriteLine(":SOEDaemon: " + message, args);
        }
    }
}
