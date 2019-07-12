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
        private static ulong nextUID = 1;
        private static ushort nextPID = 1;

        private static readonly Random Rnd = new Random();
        private static readonly MWMessagePacker Packer = new MWMessagePacker(new BinaryMWMessageEncoder());

        private static readonly List<Client> Unidentified = new List<Client>();

        private static readonly Dictionary<ulong, Client> Clients = new Dictionary<ulong, Client>();
        private static readonly Dictionary<string, Session> Sessions = new Dictionary<string, Session>();
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

                        foreach (ulong uid in Clients.Keys.ToArray())
                        {
                            Client client = Clients[uid];

                            if ((client.TcpClient != null && client.TcpClient.Connected) || !client.FullyConnected)
                            {
                                continue;
                            }

                            Console.WriteLine($"{client.Session?.Name} disconnected");
                        }

                        // Send ping messages periodically
                        if (Watch.Elapsed.TotalSeconds >= 10)
                        {
                            foreach (Client client in Unidentified.Concat(Clients.Values))
                            {
                                SendMessage(new MWPingMessage(), client);
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
                                Console.WriteLine($"Failed to read data from {client.Session?.Name}:\n{e}");
                            }
                        }
                    }
                }

                // Don't kill the CPU
                Thread.Sleep(10);
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private static bool SendMessage(MWMessage message, Client client)
        {
            if (client?.TcpClient == null || !client.TcpClient.Connected)
            {
                return false;
            }

            try
            {
                byte[] bytes = Packer.Pack(message).Buffer;

                NetworkStream stream = client.TcpClient.GetStream();
                stream.BeginWrite(bytes, 0, bytes.Length, WriteToClient, stream);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send message to '{client.Session?.Name}':\n{e}");
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

        private static string GenerateToken()
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
            (NetworkStream stream, byte[] buf, Client sender) = ((NetworkStream, byte[], Client))res.AsyncState;
            stream.EndRead(res);

            MWMessage message;
            try
            {
                message = Packer.Unpack(new MWPackedMessage(buf));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            if (message == null)
            {
                Console.Write($"Failed to unpack message: {BitConverter.ToString(buf)}");
                return;
            }

            switch (message.MessageType)
            {
                case MWMessageType.SharedCore:
                    break;
                case MWMessageType.ConnectMessage:
                    if (Unidentified.Contains(sender) && message is MWConnectMessage)
                    {
                        if (message.SenderUid == 0)
                        {
                            sender.UID = nextUID++;
                            SendMessage(new MWConnectMessage {SenderUid = sender.UID}, sender);
                            Clients.Add(sender.UID, sender);
                            Unidentified.Remove(sender);
                        }
                        else
                        {
                            Unidentified.Remove(sender);
                            sender.TcpClient.Close();
                        }
                    }

                    break;
                case MWMessageType.ReconnectMessage:
                    break;
                case MWMessageType.DisconnectMessage:
                    if (Clients.ContainsValue(sender) && message is MWDisconnectMessage)
                    {
                        sender.TcpClient.Close();
                        Clients.Remove(sender.UID);
                    }

                    break;
                case MWMessageType.JoinMessage:
                    if (Clients.ContainsKey(sender.UID) && message is MWJoinMessage joinMsg)
                    {
                        if (string.IsNullOrEmpty(joinMsg.Token))
                        {
                            string token = GenerateToken();
                            while (Sessions.ContainsKey(token))
                            {
                                token = GenerateToken();
                            }

                            sender.Session = new Session
                            {
                                Name = joinMsg.DisplayName,
                                Token = token,
                                PID = nextPID++
                            };

                            Sessions.Add(token, sender.Session);
                            sender.FullyConnected = true;

                            Console.WriteLine($"{joinMsg.DisplayName} has token {token}");
                            SendMessage(new MWJoinConfirmMessage {Token = token, DisplayName = sender.Session.Name}, sender);
                        }
                        else
                        {
                            if (!Sessions.TryGetValue(joinMsg.Token, out Session session))
                            {
                                SendMessage(new MWDisconnectMessage(), sender);
                                sender.TcpClient.Close();
                                break;
                            }

                            sender.Session = session;
                            Unidentified.Remove(sender);
                            sender.FullyConnected = true;
                        }
                    }

                    break;
                case MWMessageType.JoinConfirmMessage:
                    break;
                case MWMessageType.LeaveMessage:
                    break;
                case MWMessageType.ItemConfigurationMessage:
                    break;
                case MWMessageType.ItemConfigurationConfirmMessage:
                    break;
                case MWMessageType.ItemReceiveMessage:
                    break;
                case MWMessageType.ItemReceiveConfirmMessage:
                    break;
                case MWMessageType.ItemSendMessage:
                    break;
                case MWMessageType.ItemSendConfirmMessage:
                    break;
                case MWMessageType.NotifyMessage when message is MWNotifyMessage notify:
                    Console.WriteLine($"[{notify.From}]: {notify.Message}");
                    break;
                case MWMessageType.PingMessage:
                    break;
                case MWMessageType.InvalidMessage:
                    Console.WriteLine($"Invalid message from {GetClient(message.SenderUid)?.Session?.Name}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static Client GetClient(ulong uuid)
        {
            return Clients.TryGetValue(uuid, out Client client) ? client : null;
        }

        private class Client
        {
            public ulong UID;
            public TcpClient TcpClient;
            public bool FullyConnected;

            public Session Session;
        }

        public class Session
        {
            public string Name;
            public string Token;
            public ushort PID;
        }
    }
}
