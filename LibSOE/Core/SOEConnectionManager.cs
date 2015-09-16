using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;

using SOE.Interfaces;

namespace SOE.Core
{
    public class SOEConnectionManager
    {
        // Server component
        public SOEServer Server;

        // Connections
        private readonly List<SOEClient> Clients;
        private readonly Dictionary<IPEndPoint, int> Host2ClientID;
        private readonly Dictionary<uint, int> SessionID2ClientID;

        public SOEConnectionManager(SOEServer server)
        {
            // Server
            Server = server;

            // Our client lists
            Clients = new List<SOEClient>();
            Host2ClientID = new Dictionary<IPEndPoint, int>();
            SessionID2ClientID = new Dictionary<uint, int>();

            // Log
            Log("Service constructed");
        }

        public void AddNewClient(SOEClient newClient)
        {
            // Do they exist already?
            if (SessionID2ClientID.ContainsKey(newClient.GetSessionID()))
            {
                // Disconnect the new client
                Log("[WARNING] Someone tried connecting with the same Session ID!");
                newClient.Disconnect((ushort)SOEDisconnectReasons.ConnectFail);

                // Don't continue adding this connection
                return;
            }

            // Is there already a connection from this endpoint?
            if (Host2ClientID.ContainsKey(newClient.Client))
            {
                // Disconnect the new client
                Log("[WARNING] Someone tried connecting from the same endpoint!");
                newClient.Disconnect((ushort)SOEDisconnectReasons.ConnectFail);

                // Don't continue adding this connection
                return;
            }

            // Loop through the Clients list, looking for an open space
            int newClientId;
            for (newClientId = 0; newClientId < Clients.Count; newClientId++)
            {
                // Is this client nulled?
                if (Clients[newClientId] == null)
                {
                    // We've found an empty space!
                    break;
                }
            }

            // Set their Client ID
            newClient.SetClientID(newClientId);

            // Add them to the Clients map
            if (newClientId >= Clients.Count)
            {
                Clients.Add(newClient);
            }
            else
            {
                Clients[newClientId] = newClient;
            }

            // Add them to our maps
            Host2ClientID.Add(newClient.Client, newClientId);
            SessionID2ClientID.Add(newClient.GetSessionID(), newClientId);

            // Log
            Log("New client connection from {0}, (ID: {1})", newClient.GetClientAddress(), newClient.GetClientID());
        }

        public SOEClient GetClient(int clientId)
        {
            // Is the requested index within our List?
            if (clientId < Clients.Count)
            {
                // Return the associated client
                return Clients[clientId];
            }

            // Return a null client
            return null;
        }

        public SOEClient GetClientFromSessionID(uint sessionId)
        {
            // Does this SessionID exist?
            if (SessionID2ClientID.ContainsKey(sessionId))
            {
                // Return the associated client
                return Clients[SessionID2ClientID[sessionId]];
            }

            // Return a null client
            return null;
        }

        public SOEClient GetClientFromHost(IPEndPoint client)
        {
            // Do we have a connection from this endpoint?
            if (Host2ClientID.ContainsKey(client))
            {
                // Return the associated client
                return Clients[Host2ClientID[client]];
            }

            // Return a null client
            return null;
        }

        public void DisconnectClient(SOEClient client, ushort reason, bool clientBased = false)
        {
            // Disconnect
            Log("Disconnecting client on {0} (ID: {1}) for reason: {2}", client.GetClientAddress(), client.GetClientID(), (SOEDisconnectReasons)reason);
            
            // Are they a connected client?
            if (Clients.Contains(client))
            {
                // We don't care about them anymore
                // Open their ID as a space
                Host2ClientID.Remove(client.Client);
                SessionID2ClientID.Remove(client.GetSessionID());
                Clients[client.GetClientID()] = null;
            }

            // Was this a disconnect request from the client itself?
            if (!clientBased)
            {
                // Tell them we're disconnecting them
                SOEWriter packetWriter = new SOEWriter((ushort)SOEOPCodes.DISCONNECT);

                // Arguments
                packetWriter.AddUInt32(client.GetSessionID());
                packetWriter.AddUInt16(reason);

                // Send!
                SOEPacket packet = packetWriter.GetFinalSOEPacket(client, false, false);
                client.SendPacket(packet);
            }
        }

        public void StartKeepAliveThread()
        {
            Thread keepAliveThread = new Thread((threadStart3) =>
            {
                while (Server.Running)
                {
                    // Get a Now time for this cycle
                    int now = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

                    // Loop through the clients
                    for (int i = 0; i < Clients.Count; i++)
                    {
                        // Client
                        SOEClient client = GetClient(i);

                        // Empty space?
                        if (client == null)
                        {
                            continue;
                        }

                        // Idle?
                        if (now > (client.GetLastInteraction() + Server.CLIENT_TIMEOUT))
                        {
                            Log("Disconnecting Idle client.");
                            client.Disconnect((ushort)SOEDisconnectReasons.Timeout);
                        }
                    }

                    Thread.Sleep(Server.SERVER_THREAD_SLEEP);
                }
            });
            keepAliveThread.Name = "SOEServer::KeepAliveThread";
            keepAliveThread.Start();
        }

        public void Log(string message, params object[] args)
        {
            Console.WriteLine(":SOEConnectionManager: " + message, args);
        }
    }
}
