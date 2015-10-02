using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using SOE.Interfaces;

namespace SOE.Core
{
    public struct SOEPendingPacket
    {
        public SOEClient Client;
        public byte[] Packet;

        public SOEPendingPacket(SOEClient sender, byte[] packet)
        {
            Client = sender;
            Packet = packet;
        }
    }

    public struct SOEPendingMessage
    {
        public SOEClient Client;
        public byte[] Message;

        public SOEPendingMessage(SOEClient sender, byte[] packet)
        {
            Client = sender;
            Message = packet;
        }
    }

    public class SOEServer
    {
        // Server components
        private readonly UdpClient UdpClient;
        public readonly SOEConnectionManager ConnectionManager;
        public readonly SOEProtocol Protocol;

        private readonly ConcurrentQueue<SOEPendingPacket> IncomingPackets;
        private readonly ConcurrentQueue<SOEPendingMessage> IncomingMessages;
        
        // Server variables
        public bool Running = true;

        // Settings
        public Dictionary<string, dynamic> Configuration = new Dictionary<string, dynamic>
        {
            // Basic information
            {"Name", "SOEServer"},
            {"Port", 20260},
            {"ID", 4001},
            {"Roles", new object[0]},

            // Application settings
            {"AppName", "Sony Online"},
            {"ShortAppName", "SOE"},

            // Threading toggles
            {"WantDynamicThreading", true},
            {"WantPacketThreading", true},
            {"WantMessageThreading", true},

            // Threading settings
            {"ServerThreadSleep", 13},
            {"MinThreadPoolSize", 2},
            {"MaxThreadPoolSize", 8}
        };

        public SOEServer(Dictionary<string, dynamic> configuration)
        {
            // Server components
            ConnectionManager = new SOEConnectionManager(this);
            Protocol = new SOEProtocol(this, "CGAPI_257");

            // Configure!
            foreach (var configVariable in configuration)
            {
                if (!Configuration.ContainsKey(configVariable.Key))
                {
                    // Is it a component?
                    switch (configVariable.Key)
                    {
                        case "ConnectionManager":
                            ConnectionManager.Configure(configuration["ConnectionManager"]);
                            break;

                        case "Protocol":
                            // Protocol.Configure(configuration["Protocol"]);
                            break;

                        case "Logger":
                            break;

                        case "Application":
                            break;

                        default:
                            // Bad configuration variable
                            Log("Invalid configuration variable '{0}' for SOEServer instance. Ignoring.", configVariable.Key);
                            break;
                    }

                    // Continue!
                    continue;
                }

                // Set this variable
                Configuration[configVariable.Key] = configVariable.Value;
            }

            // Get variables
            int port = Configuration["Port"];

            // Log
            Log("Initiating server on port: {0}", port);

            // UDP Listener
            UdpClient = new UdpClient(port);

            IncomingPackets = new ConcurrentQueue<SOEPendingPacket>();
            IncomingMessages = new ConcurrentQueue<SOEPendingMessage>();

            // Initialize our message handlers
            Log("Initializing message handlers");
            MessageHandlers.Initialize();
            Log("Initiated server");
        }

        private void DoNetCycle()
        {
            // Get variables
            bool wantPacketThreading = Configuration["WantPacketThreading"];
            int port = Configuration["Port"];

            // Receive a packet
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, port);
            byte[] rawPacket;

            try
            {
                rawPacket = UdpClient.Receive(ref sender);
            }
            catch (SocketException)
            {
                // Maybe we just killed the client?
                return;
            }

            // Get the associated client (or create a new fake one)
            SOEClient client = ConnectionManager.GetClientFromHost(sender);
            if (client == null)
            {
                // Make a fake client for new connections
                client = new SOEClient(ConnectionManager, sender);
            }

            // Do we wanna handle this, or give it to our workers?
            if (wantPacketThreading)
            {
                // Put it in the queue for our workers..
                IncomingPackets.Enqueue(new SOEPendingPacket(client, rawPacket));
            }
            else
            {
                // Handle the packet
                Protocol.HandlePacket(client, rawPacket);
            }
        }

        public void SendPacket(SOEClient client, SOEPacket packet)
        {
            // Send the message
            UdpClient.Send(packet.GetRaw(), packet.GetLength(), client.Client);
        }

        public void ReceiveMessage(SOEClient sender, byte[] rawMessage)
        {
            // Get variables
            bool wantMessageThreading = Configuration["WantMessageThreading"];

            if (wantMessageThreading)
            {
                IncomingMessages.Enqueue(new SOEPendingMessage(sender, rawMessage));
            }
            else
            {
                Protocol.HandleMessage(sender, rawMessage);
            }
        }

        public void Run()
        {
            // Get variables
            bool wantPacketThreading = Configuration["WantPacketThreading"];
            bool wantMessageThreading = Configuration["WantMessageThreading"];
            int maxThreadPoolSize = Configuration["MaxThreadPoolSize"];
            int threadSleep = Configuration["ServerThreadSleep"];

            // Server threads
            Log("Starting server threads");
            Thread netThread = new Thread((threadStart) =>
            {
                while (Running)
                {
                    // Do a cycle
                    DoNetCycle();

                    // Sleep
                    Thread.Sleep(threadSleep);
                }
            });
            netThread.Name = "SOEServer::NetThread";
            netThread.Start();

            // Create the packet worker threads
            if (wantPacketThreading)
            {
                for (int i = 0; i < maxThreadPoolSize; i++)
                {
                    Thread workerThread = new Thread((workerThreadStart) =>
                    {
                        while (Running)
                        {
                            // Get a packet and handle it.
                            SOEPendingPacket packet;

                            if (IncomingPackets.TryDequeue(out packet))
                            {
                                Protocol.HandlePacket(packet.Client, packet.Packet);
                            }

                            // Sleep
                            Thread.Sleep(threadSleep);
                        }
                    });

                    workerThread.Name = string.Format("SOEServer::PacketWorkerThread{0}", i + 1);
                    workerThread.Start();
                }
            }

            // Create the message worker threads
            if (wantMessageThreading)
            {
                for (int i = 0; i < maxThreadPoolSize; i++)
                {
                    Thread workerThread = new Thread((workerThreadStart) =>
                    {
                        while (Running)
                        {
                            // Get a packet and handle it.
                            SOEPendingMessage message;

                            if (IncomingMessages.TryDequeue(out message))
                            {
                                Protocol.HandleMessage(message.Client, message.Message);
                            }

                            // Sleep
                            Thread.Sleep(threadSleep);
                        }
                    });

                    workerThread.Name = string.Format("SOEServer::MessageWorkerThread{0}", i + 1);
                    workerThread.Start();
                }
            }

            // Create the idle connection thread
            ConnectionManager.StartKeepAliveThread();

            // Done
            Log("Started listening");
        }

        public void Stop()
        {
            Running = false;
        }

        public void Log(string message, params object[] args)
        {
            Console.WriteLine(":SOEServer: " + message, args);
        }
    }
}
