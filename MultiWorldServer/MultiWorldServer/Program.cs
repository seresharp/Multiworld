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

        private static readonly Stopwatch Watch = new Stopwatch();

        private static void Main()
        {
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

    }
}
