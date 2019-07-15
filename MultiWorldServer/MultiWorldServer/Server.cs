using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Binary;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using MultiWorldProtocol.Messaging.Definitions.Messages;

namespace MultiWorldServer
{
    class Server
    {
        private ulong nextUID = 1;
        private ushort nextPID = 0;
        private readonly MWMessagePacker Packer = new MWMessagePacker(new BinaryMWMessageEncoder());
        private readonly List<Client> Unidentified = new List<Client>();

        private readonly Timer PingTimer;

        private readonly object _clientLock = new object();
        private readonly Dictionary<ulong, Client> Clients = new Dictionary<ulong, Client>();
        private readonly Dictionary<string, Session> Sessions = new Dictionary<string, Session>();
        private TcpListener _server;
        private readonly Thread _readThread;
        private readonly Timer ResendTimer;

        private readonly ServerSettings _settings;
        private Dictionary<string, (int, string)>[] _itemPlacements;

        public Server(int port, ServerSettings settings)
        {
            _settings = settings;
            _itemPlacements = MultiworldRandomizer.Randomize(_settings);

            //Listen on any ip
            _server = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
            _server.Start();

            _readThread = new Thread(ReadWorker);
            _readThread.Start();
            _server.BeginAcceptTcpClient(AcceptClient, _server);
            PingTimer = new Timer(DoPing, Clients, 1000, 1000);
            ResendTimer = new Timer(DoResends, Clients, 500, 1000);

            Console.WriteLine("Server started!");
        }

        private void DoPing(object clients)
        {
            lock (_clientLock)
            {
                List<Client> clientList = Clients.Values.ToList();
                for (int i = clientList.Count - 1; i >= 0; i--)
                {
                    Client client = clientList[i];
                    SendMessage(new MWPingMessage(), client);
                }
            }
        }

        private void DoResends(object clients)
        {
            lock (_clientLock)
            {
                foreach (Client client in Clients.Values)
                {
                    if(client.Session!= null)
                    {
                        lock (client.Session.MessagesToConfirm)
                        {
                            var now = DateTime.Now;
                            foreach (ResendEntry entry in client.Session.MessagesToConfirm)
                            {
                                if (now - entry.LastSent > TimeSpan.FromSeconds(5))
                                {
                                    var msg = entry.Message;
                                    SendMessage(msg, client);
                                    entry.LastSent = now;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ReadWorker()
        {
            //Allocating outside of the loop to not reallocate every time
            List<(Client, MWPackedMessage)> messages = new List<(Client, MWPackedMessage)>();
            while (true)
            {
                messages.Clear();
                lock (_clientLock)
                {
                    foreach (Client client in Unidentified.Concat(Clients.Values))
                    {
                        if (client.TcpClient.Available > 0)
                        {
                            NetworkStream stream = client.TcpClient.GetStream();
                            messages.Add((client, new MWPackedMessage(stream)));
                        }
                    }
                }

                foreach ((Client client, MWPackedMessage message) in messages)
                {
                    ReadFromClient(client, message);
                }

                Thread.Sleep(10);
            }
        }

        private void AcceptClient(IAsyncResult res)
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

            lock (_clientLock)
            {
                Unidentified.Add(client);
            }
        }

        private bool SendMessage(MWMessage message, Client client)
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
                Console.WriteLine($"Failed to send message to '{client.Session?.Name}':\n{e}\nDisconnecting");
                DisconnectClient(client);
                return false;
            }
        }

        private static void WriteToClient(IAsyncResult res)
        {
            NetworkStream stream = (NetworkStream)res.AsyncState;
            stream.EndWrite(res);
        }

        private void DisconnectClient(Client client)
        {
            SendMessage(new MWDisconnectMessage(), client);
            //Wait a bit to give the message a chance to be sent at least before closing the client
            Thread.Sleep(10);
            client.TcpClient.Close();
            lock (_clientLock)
                Clients.Remove(client.UID);
        }

        private void ReadFromClient(Client sender, MWPackedMessage packed)
        {
            MWMessage message;
            try
            {
                message = Packer.Unpack(packed);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            switch (message.MessageType)
            {
                case MWMessageType.SharedCore:
                    break;
                case MWMessageType.ConnectMessage:
                    HandleConnect(sender, (MWConnectMessage)message);
                    break;
                case MWMessageType.ReconnectMessage:
                    break;
                case MWMessageType.DisconnectMessage:
                    HandleDisconnect(sender, (MWDisconnectMessage)message);
                    break;
                case MWMessageType.JoinMessage:
                    HandleJoin(sender, (MWJoinMessage)message);
                    break;
                case MWMessageType.JoinConfirmMessage:
                    break;
                case MWMessageType.LeaveMessage:
                    break;
                case MWMessageType.ItemConfigurationMessage:
                    break;
                case MWMessageType.ItemConfigurationConfirmMessage:
                    HandleConfigurationConfirm(sender, (MWItemConfigurationConfirmMessage)message);
                    break;
                case MWMessageType.ItemReceiveMessage:
                    break;
                case MWMessageType.ItemReceiveConfirmMessage:
                    HandleItemReceiveConfirm(sender, (MWItemReceiveConfirmMessage)message);
                    break;
                case MWMessageType.ItemSendMessage:
                    HandleItemSend(sender, (MWItemSendMessage)message);
                    break;
                case MWMessageType.ItemSendConfirmMessage:
                    break;
                case MWMessageType.NotifyMessage:
                    HandleNotify(sender, (MWNotifyMessage)message);
                    break;
                case MWMessageType.PingMessage:
                    break;
                case MWMessageType.ItemConfigurationRequestMessage:
                    HandleItemConfigurationRequest(sender, (MWItemConfigurationRequestMessage)message);
                    break;
                case MWMessageType.InvalidMessage:
                default:
                    throw new InvalidOperationException("Received Invalid Message Type");
            }
        }

        private void HandleConnect(Client sender, MWConnectMessage message)
        {
            lock (_clientLock)
            {
                if (Unidentified.Contains(sender))
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
            }
        }

        private void HandleDisconnect(Client sender, MWDisconnectMessage message)
        {
            DisconnectClient(sender);
        }

        private void HandleJoin(Client sender, MWJoinMessage message)
        {
            lock (_clientLock)
            {
                if (!Clients.ContainsKey(sender.UID))
                {
                    return;
                }

                if (string.IsNullOrEmpty(message.Token))
                {
                    if (Clients.Count(client => client.Value.FullyConnected) >= _settings.Players)
                    {
                        sender.TcpClient.Close();
                        return;
                    }

                    while (sender.Session == null || Sessions.ContainsKey(sender.Session.Token))
                    {
                        sender.Session = new Session(message.DisplayName) {PID = nextPID++};
                    }

                    Sessions.Add(sender.Session.Token, sender.Session);
                    sender.FullyConnected = true;

                    Console.WriteLine($"{message.DisplayName} has token {sender.Session.Token}");
                    SendMessage(new MWJoinConfirmMessage { Token = sender.Session.Token, DisplayName = sender.Session.Name }, sender);
                }
                else
                {
                    if (!Sessions.TryGetValue(message.Token, out Session session))
                    {
                        SendMessage(new MWDisconnectMessage(), sender);
                        sender.TcpClient.Close();
                        return;
                    }

                    sender.Session = session;
                    sender.FullyConnected = true;
                }

                IEnumerable<Client> connected =
                    Clients.Where(client => client.Value.FullyConnected).Select(client => client.Value);

                if (connected.Count() >= _settings.Players)
                {
                    foreach (Client c in connected)
                    {
                        foreach (string loc in _itemPlacements[c.Session.PID].Keys)
                        {
                            (int player, string item) = _itemPlacements[c.Session.PID][loc];

                            ConfigureItem(c, loc, item, (ushort)player);
                        }
                    }
                }
            }
        }

        private void HandleNotify(Client sender, MWNotifyMessage message)
        {
            Console.WriteLine($"[{sender.Session?.Name}]: {message.Message}");
        }


        private void HandleConfigurationConfirm(Client sender, MWItemConfigurationConfirmMessage message)
        {
            sender.Session.ConfirmMessage(message);
        }

        private void HandleItemReceiveConfirm(Client sender, MWItemReceiveConfirmMessage message)
        {
            sender.Session.ConfirmMessage(message);
        }

        private void HandleItemSend(Client sender, MWItemSendMessage message)
        {
            //Confirm sending the item to the sender
            SendMessage(new MWItemSendConfirmMessage {Item = message.Item, To=message.To}, sender);
            lock (sender.Session.PickedUpLocations)
            {
                if (!sender.Session.PickedUpLocations.Contains(message.Location))
                {
                    sender.Session.PickedUpLocations.Add(message.Location);
                    SendItemTo(message.To, message.Item, sender.Session.Name);
                }
            }
        }

        private void SendItemTo(uint player, string Item, string From)
        {
            lock (_clientLock)
            {
                foreach (Client c in Clients.Values)
                {
                    if (c.Session.PID == player)
                    {
                        c.Session.QueueConfirmableMessage(new MWItemReceiveMessage { From = From, Item = Item });
                        return;
                    }
                }
            }
        }

        private void HandleItemConfigurationRequest(Client sender, MWItemConfigurationRequestMessage msg)
        {
            if (sender.FullyConnected && sender.Session != null)
            {
                foreach (string loc in _itemPlacements[sender.Session.PID].Keys)
                {
                    (int player, string item) = _itemPlacements[sender.Session.PID][loc];

                    ConfigureItem(sender, loc, item, (ushort)player);
                }
            }
        }

        public void ConfigureItem(Client c, string Location, string Item, ushort player)
        {
            c.Session.QueueConfirmableMessage(new MWItemConfigurationMessage { Location = Location, Item = Item, PlayerId = player});
        }

        private Client GetClient(ulong uuid)
        {
            lock (_clientLock)
            {
                return Clients.TryGetValue(uuid, out Client client) ? client : null;
            }
        }
    }
}
