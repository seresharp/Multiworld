using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiWorldProtocol.Binary;
using MultiWorldProtocol.Messaging;
using System.Diagnostics;
using MultiWorldProtocol.Messaging.Definitions.Messages;
using System.Net.Sockets;
using System.Threading;

namespace MultiWorldClient
{
    class ClientConnection
    {
        private readonly MWMessagePacker Packer = new MWMessagePacker(new BinaryMWMessageEncoder());
        private TcpClient _client;
        private readonly Timer PingTimer;
        private ConnectionState State;
        private List<MWItemSendMessage> ItemSendQueue = new List<MWItemSendMessage>();
        private Thread ReadThread;

        public ClientConnection(string host, int port, string Username)
        {
            State = new ConnectionState();
            State.UserName = Username;
            PingTimer = new Timer(DoPing, State, 1000, 1000);

            _client = new TcpClient
            {
                ReceiveTimeout = 2000,
                SendTimeout = 2000
            };

            _client.Connect(host, port);
            SendMessage(new MWConnectMessage { });
            Console.WriteLine("Success!");
            ReadThread = new Thread(new ThreadStart(ReadWorker));
            ReadThread.Start();
        }

        private void DoPing(object state)
        {
            if(State.Connected)
            {
                SendMessage(new MWPingMessage());
                //If there are items in the queue that the server hasn't confirmed yet
                if(ItemSendQueue.Count>0 && State.Joined)
                {
                    ResendItemQueue();
                }
            }
        }

        private void SendMessage(MWMessage msg)
        {
            try
            {
                //Always set Uid in here, if uninitialized will be 0 as required.
                //Otherwise less work resuming session etc.
                msg.SenderUid = State.Uid;
                byte[] bytes = Packer.Pack(msg).Buffer;
                NetworkStream stream = _client.GetStream();
                stream.BeginWrite(bytes, 0, bytes.Length, WriteToServer, stream);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send message '{msg}' to server:\n{e}");
            }
        }

        private void ReadWorker()
        {
            while (!_client.Connected)
            {
                Thread.Sleep(100);
            }

            NetworkStream stream = _client.GetStream();
            while(true)
            {
                var message = new MWPackedMessage(stream);
                ReadFromServer(message);
            }
        }

        private void WriteToServer(IAsyncResult res)
        {
            NetworkStream stream = (NetworkStream)res.AsyncState;
            stream.EndWrite(res);
        }

        private void ReadFromServer(MWPackedMessage packed)
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
                    HandleConnect((MWConnectMessage)message);
                    break;
                case MWMessageType.ReconnectMessage:
                    break;
                case MWMessageType.DisconnectMessage:
                    HandleDisconnectMessage((MWDisconnectMessage)message);
                    break;
                case MWMessageType.JoinMessage:
                    break;
                case MWMessageType.JoinConfirmMessage:
                    HandleJoinConfirm((MWJoinConfirmMessage)message);
                    break;
                case MWMessageType.LeaveMessage:
                    HandleLeaveMessage((MWLeaveMessage)message);
                    break;
                case MWMessageType.ItemConfigurationMessage:
                    HandleItemConfiguration((MWItemConfigurationMessage)message);
                    break;
                case MWMessageType.ItemConfigurationConfirmMessage:
                    break;
                case MWMessageType.ItemReceiveMessage:
                    HandleItemReceive((MWItemReceiveMessage)message);
                    break;
                case MWMessageType.ItemReceiveConfirmMessage:
                    break;
                case MWMessageType.ItemSendMessage:
                    break;
                case MWMessageType.ItemSendConfirmMessage:
                    HandleItemSendConfirm((MWItemSendConfirmMessage)message);
                    break;
                case MWMessageType.NotifyMessage:
                    HandleNotify((MWNotifyMessage)message);
                    break;
                case MWMessageType.PingMessage:
                    break;
                case MWMessageType.InvalidMessage:
                default:
                    throw new InvalidOperationException("Received Invalid Message Type");
            }

            Console.WriteLine($"Server: {message}");
        }

        private void ResendItemQueue()
        {
            foreach(MWItemSendMessage message in ItemSendQueue)
            {
                SendMessage(message);
            }
        }

        private void ClearFromSendQueue(uint playerId, string item)
        {
            for(int i=ItemSendQueue.Count-1; i>=0; i--)
            {
                var queueItem = ItemSendQueue[i];
                if (playerId == queueItem.To && item == queueItem.Item)
                    ItemSendQueue.RemoveAt(i);
            }
        }

        private void HandleConnect(MWConnectMessage message)
        {
            State.Uid = message.SenderUid;
            State.Connected = true;
            Console.WriteLine("Connected");
            SendMessage(new MWJoinMessage { DisplayName = State.UserName, Token = "" });
        }

        private void HandleJoinConfirm(MWJoinConfirmMessage message)
        {
            State.Token = message.Token;
            State.Joined = true;
            Console.WriteLine("Joined");
            State.GameInfo = new GameInformation(message.PlayerId);
        }

        private void HandleItemConfiguration(MWItemConfigurationMessage message)
        {
            State.GameInfo.SetLocation(message.Location, message.Item, message.PlayerId);
            SendMessage(new MWItemConfigurationConfirmMessage { Location = message.Location, Item = message.Item, PlayerId = message.PlayerId });
        }

        private void HandleLeaveMessage(MWLeaveMessage message)
        {
            State.Joined = false;
        }

        private void HandleDisconnectMessage(MWDisconnectMessage message)
        {
            State.Connected = false;
        }

        private void HandleNotify(MWNotifyMessage message)
        {
            //Do whatever we want to do with notifies here
        }

        private void HandleItemReceive(MWItemReceiveMessage message)
        {
            //Do whatever we want to do when we get an item here, then confirm
            SendMessage(new MWItemReceiveConfirmMessage { Item = message.Item, From = message.From });
        }

        private void HandleItemSendConfirm(MWItemSendConfirmMessage message)
        {
            ClearFromSendQueue(message.To, message.Item);
        }

        public void Say(string message)
        {
            SendMessage(new MWNotifyMessage { Message = message, To = "All", From = State.UserName });
        }

        public void SendItem(string item, uint playerId)
        {
            ItemSendQueue.Add(new MWItemSendMessage { Item = item, To = playerId });
        }
    }
}
