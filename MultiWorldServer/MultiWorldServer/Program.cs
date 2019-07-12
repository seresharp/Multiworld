using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MultiWorldProtocol.Binary;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions.Messages;

namespace MultiWorldServer
{
    internal class Program
    {
        private static readonly Random Rnd = new Random();

        private static readonly List<Client> Unidentified = new List<Client>();

        private static readonly Dictionary<string, Client> Clients = new Dictionary<string, Client>();
        private static TcpListener _server = new TcpListener(IPAddress.Parse("127.0.0.1"), 5001);
        private static readonly Stopwatch Watch = new Stopwatch();

        private static void Main()
        {
            Console.WriteLine("Enter IP and port in the form '127.0.0.1:5001'");

            string[] input = Console.ReadLine().Split(':');
            _server = new TcpListener(IPAddress.Parse(input[0]), int.Parse(input[1]));

            _server.Start();

            Console.WriteLine("Server started!");

            _server.BeginAcceptTcpClient(AcceptClient, _server);

            Watch.Start();

            while (true)
            {
                lock (Unidentified)
                {
                    lock (Clients)
                    {
                        // Remove inactive connections
                        foreach (Client client in Unidentified.ToArray())
                        {
                            if (client?.TcpClient == null || !client.TcpClient.Connected)
                            {
                                Unidentified.Remove(client);
                            }
                        }

                        foreach (string key in Clients.Keys.ToArray())
                        {
                            Client client = Clients[key];

                            if ((client.TcpClient != null && client.TcpClient.Connected) || !client.FullyConnected)
                            {
                                continue;
                            }

                            Console.WriteLine($"{client.Name} disconnected");
                            client.ResetClient();
                        }

                        // Send ping messages periodically
                        if (Watch.Elapsed.TotalSeconds >= 10)
                        {
                            foreach (Client client in Unidentified.Concat(Clients.Values))
                            {
                                SendMessage("PING", client);
                            }

                            Watch.Reset();
                            Watch.Start();
                        }

                        // Read data from clients
                        foreach (Client client in Unidentified.Concat(Clients.Values))
                        {
                            if (client?.TcpClient == null || !client.TcpClient.Connected || client.TcpClient.Available == 0)
                            {
                                continue;
                            }

                            byte[] buf = new byte[client.TcpClient.Available];

                            try
                            {
                                NetworkStream stream = client.TcpClient.GetStream();
                                stream.BeginRead(buf, 0, buf.Length, ReadFromClient, (stream, buf, client));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Failed to read data from {client.Name}:\n{e}");
                            }
                        }
                    }
                }

                // Don't kill the CPU
                Thread.Sleep(10);
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private static bool SendMessage(string msg, Client client)
        {
            if (client?.TcpClient == null || !client.TcpClient.Connected)
            {
                return false;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(msg);

            try
            {
                NetworkStream stream = client.TcpClient.GetStream();
                stream.BeginWrite(bytes, 0, bytes.Length, WriteToClient, stream);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send message '{msg}' to '{client.Name}':\n{e}");
                return false;
            }
        }

        private static void AcceptClient(IAsyncResult res)
        {
            Client client = new Client
            {
                TcpClient = _server.EndAcceptTcpClient(res)
            };

            _server.BeginAcceptTcpClient(AcceptClient, _server);

            if (!client.TcpClient.Connected)
            {
                return;
            }

            client.TcpClient.ReceiveTimeout = 2000;
            client.TcpClient.SendTimeout = 2000;

            lock (Unidentified)
            {
                Unidentified.Add(client);
            }
        }

        private static string GenerateUUID()
        {
            byte[] bytes = new byte[16];

            for (int i = 0; i < 16; i++)
            {
                bytes[i] = (byte)Rnd.Next(33, 126);
            }

            return Encoding.ASCII.GetString(bytes);
        }

        private static void WriteToClient(IAsyncResult res)
        {
            NetworkStream stream = (NetworkStream) res.AsyncState;
            stream.EndWrite(res);
        }

        private static void ReadFromClient(IAsyncResult res)
        {
            (NetworkStream stream, byte[] buf, Client player) = ((NetworkStream, byte[], Client))res.AsyncState;
            stream.EndRead(res);

            string message = Encoding.ASCII.GetString(buf);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            string[] msgArgs = message.Split(' ');
            if (msgArgs.Length == 0)
            {
                return;
            }

            switch (msgArgs[0])
            {
                case "PING":
                    // Do nothing, sending back PONG is unnecessary due to TCP acknowledge
                    break;
                case "SAY" when msgArgs.Length > 1 && Clients.ContainsValue(player):
                    for (int i = 1; i < msgArgs.Length; i++)
                    {
                        Console.Write(msgArgs[i] + " ");
                    }

                    Console.WriteLine();
                    break;
                case "GIVE" when msgArgs.Length == 3 && Clients.ContainsValue(player):
                    if (!Clients.TryGetValue(msgArgs[1], out Client itemClient))
                    {
                        break;
                    }

                    if (SendMessage($"GET {msgArgs[2]}", itemClient))
                    {
                        Console.WriteLine($"{itemClient.Name} received '{msgArgs[2]}' from {player.Name}");
                    }

                    break;
                case "NAME" when msgArgs.Length == 2:
                    if (Unidentified.Concat(Clients.Values).Any(otherClient => otherClient.Name?.ToLower() == msgArgs[1].ToLower()))
                    {
                        SendMessage($"Name {msgArgs[1]} is already in use", player);
                        break;
                    }

                    if (!Unidentified.Contains(player))
                    {
                        Console.WriteLine($"{player.Name} changed name to {msgArgs[1]}");
                        player.Name = msgArgs[1];
                    }
                    else
                    {
                        player.Name = msgArgs[1];

                        if (player.UUID != null)
                        {
                            Clients.Add(player.UUID, player);
                            Unidentified.Remove(player);
                            player.FullyConnected = true;

                            Console.WriteLine($"{player.Name} connected");
                        }
                    }

                    break;
                case "UUID" when msgArgs.Length == 1 && Unidentified.Contains(player):
                    string uuid = GenerateUUID();
                    while (Clients.TryGetValue(uuid, out _))
                    {
                        uuid = GenerateUUID();
                    }

                    SendMessage($"UUID {uuid}", player);
                    player.UUID = uuid;
                    if (player.Name != null)
                    {
                        Clients.Add(uuid, player);
                        Unidentified.Remove(player);
                        player.FullyConnected = true;

                        Console.WriteLine($"{player.Name} connected");
                    }

                    break;
                case "UUID" when msgArgs.Length == 2 && Unidentified.Contains(player):
                    if (!Clients.TryGetValue(msgArgs[1], out Client client))
                    {
                        SendMessage($"No client found with UUID '{msgArgs[1]}'", player);
                        break;
                    }

                    if (client.TcpClient.Connected)
                    {
                        SendMessage("You are already connected to the server", player);
                        break;
                    }

                    client.TcpClient = player.TcpClient;
                    Unidentified.Remove(player);
                    client.FullyConnected = true;
                    Console.WriteLine($"{client.Name} reconnected");

                    break;
                default:
                    Console.WriteLine($"Improper command from {player.Name}: '{message}'");
                    break;
            }
        }

        private class Client
        {
            public string UUID;
            public string Name;
            public TcpClient TcpClient;
            public bool FullyConnected;

            public void ResetClient()
            {
                TcpClient?.Close();
                TcpClient = new TcpClient();
                FullyConnected = false;
            }
        }
    }
}
