using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading;

using SOE.Interfaces;
using SOE.Database;

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
        public readonly SOEConnectionManager ConnectionManager;
        public readonly SOEDatabaseManager DatabaseManager;
        public readonly SOEProtocol Protocol;
        private readonly UdpClient UdpClient;

        // Threading packets/messages
        private readonly ConcurrentQueue<SOEPendingPacket> IncomingPackets;
        private readonly ConcurrentQueue<SOEPendingMessage> IncomingMessages;
        
        // Server variables
        public readonly bool Running = true;
        private readonly int Port = 0;

        // Settings
        public string GAME_NAME = "SOE";

        public int CLIENT_TIMEOUT = 15;
        public int SERVER_THREAD_SLEEP = 13;

        public int THREAD_POOL_SIZE = 8;
        public bool WANT_PACKET_THREADING = true;
        public bool WANT_MESSAGE_THREADING = true;

        public SOEServer(int port, string protocol="SOE")
        {
            // Log
            Log("Initiating server on port: {0}", port);

            // UDP Listener
            UdpClient = new UdpClient(port);
            Port = port;

            // Server components
            ConnectionManager = new SOEConnectionManager(this);
            DatabaseManager = new SOEDatabaseManager(this);
            Protocol = new SOEProtocol(this, protocol);

            IncomingPackets = new ConcurrentQueue<SOEPendingPacket>();
            IncomingMessages = new ConcurrentQueue<SOEPendingMessage>();

            // Initialize our message handlers
            MessageHandlers.Initialize();
            Log("Initiated server");
        }

        private void DoNetCycle()
        {
            // Receive a packet
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, Port);
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
            if (WANT_PACKET_THREADING)
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
            if (WANT_MESSAGE_THREADING)
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
            // Server threads
            Log("Starting server threads");
            Thread netThread = new Thread((threadStart) =>
            {
                while (Running)
                {
                    // Do a cycle
                    DoNetCycle();

                    // Sleep
                    Thread.Sleep(SERVER_THREAD_SLEEP);
                }
            });
            netThread.Name = "SOEServer::NetThread";
            netThread.Start();

            // Create the packet worker threads
            if (WANT_PACKET_THREADING)
            {
                for (int i = 0; i < THREAD_POOL_SIZE; i++)
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
                            Thread.Sleep(SERVER_THREAD_SLEEP);
                        }
                    });

                    workerThread.Name = string.Format("SOEServer::PacketWorkerThread{0}", i + 1);
                    workerThread.Start();
                }
            }

            // Create the message worker threads
            if (WANT_PACKET_THREADING)
            {
                for (int i = 0; i < THREAD_POOL_SIZE; i++)
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
                            Thread.Sleep(SERVER_THREAD_SLEEP);
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

        public void Log(string message, params object[] args)
        {
            Console.WriteLine(":SOEServer: " + message, args);
        }
    }
}
