using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiWorldProtocol.Binary;
using MultiWorldProtocol.Messaging;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using MultiWorldProtocol.Messaging.Definitions.Messages;
using System.Net.Sockets;
using System.Threading;
using Modding;
using UnityEngine;

namespace MultiWorldMod
{
    public class ClientConnection
    {
        private readonly MWMessagePacker Packer = new MWMessagePacker(new BinaryMWMessageEncoder());
        private TcpClient _client;
        private readonly Timer PingTimer;
        private ConnectionState State;
        private List<MWItemSendMessage> ItemSendQueue = new List<MWItemSendMessage>();
        private Thread ReadThread;

        public delegate void ItemReceiveEvent(string from, string itemName);

        public delegate void MessageReceiveEvent(string from, string message);

        public event ItemReceiveEvent ItemReceived;
        public event MessageReceiveEvent MessageReceived;

        private List<MWMessage> messageEventQueue = new List<MWMessage>();

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
            MultiWorldMod.Instance.Log("Success!");
            ReadThread = new Thread(new ThreadStart(ReadWorker));
            ReadThread.Start();

            ModHooks.Instance.HeroUpdateHook += SynchronizeEvents;
        }

        private void SynchronizeEvents()
        {
            MWMessage message = null;

            lock (messageEventQueue)
            {
                if (messageEventQueue.Count > 0)
                {
                    message = messageEventQueue[0];
                    messageEventQueue.RemoveAt(0);
                }
            }

            if (message == null)
            {
                return;
            }

            switch (message)
            {
                case MWNotifyMessage notify:
                    MessageReceived?.Invoke(notify.From, notify.Message);
                    break;
                case MWItemReceiveMessage item:
                    ItemReceived?.Invoke(item.From, item.Item);
                    break;
                default:
                    MultiWorldMod.Instance.Log("Unknown type in message queue: " + message.MessageType);
                    break;
            }
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
                MultiWorldMod.Instance.Log($"Failed to send message '{msg}' to server:\n{e}");
            }
        }

        private void ReadWorker()
        {
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
                MultiWorldMod.Instance.Log(e);
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
            SendMessage(new MWJoinMessage { DisplayName = State.UserName, Token = "" });
        }

        private void HandleJoinConfirm(MWJoinConfirmMessage message)
        {
            State.Token = message.Token;
            State.Joined = true;
            State.GameInfo = new GameInformation(message.PlayerId);
        }

        private void HandleItemConfiguration(MWItemConfigurationMessage message)
        {
            MultiWorldMod.Instance.Log(message.Location + " is " + message.Item + " for player " + message.PlayerId);

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
            lock (messageEventQueue)
            {
                messageEventQueue.Add(message);
            }
        }

        private void HandleItemReceive(MWItemReceiveMessage message)
        {
            lock (messageEventQueue)
            {
                messageEventQueue.Add(message);
            }

            GiveItem(message.Item);

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

        public bool GetItemAtLocation(string loc, out PlayerItem item)
        {
            return State.GameInfo.ItemLocations.TryGetValue(loc, out item);
        }

        public PlayerItem[] GetItemsInShop(string shopName)
        {
            List<PlayerItem> items = new List<PlayerItem>();

            int i = 0;
            while (GetItemAtLocation(shopName + "_" + (i++), out PlayerItem item))
            {
                items.Add(item);
            }

            return items.ToArray();
        }

        public void ObtainItem(string loc)
        {
            File.WriteAllLines(Application.persistentDataPath + "/test.txt", new [] {"Hello world"});
            MultiWorldMod.Instance.Log("Here");

            if (!GetItemAtLocation(loc, out PlayerItem item))
            {
                MultiWorldMod.Instance.Log("Location " + loc + " not found");
                return;
            }

            MultiWorldMod.Instance.Log("Here 2");

            if (item.PlayerId != State.GameInfo.PlayerID)
            {
                MultiWorldMod.Instance.Log("Giving item " + item.Item + " to player " + item.PlayerId);
                ItemSendQueue.Add(new MWItemSendMessage {Item = item.Item, Location = loc, To = item.PlayerId});
                return;
            }

            GiveItem(item.Item);
        }

        public void ObtainShopItem(string shopName, string itemName)
        {
            int i = 0;
            while (GetItemAtLocation(shopName + "_" + (i++), out PlayerItem item))
            {
                if (item.Item == itemName)
                {
                    ObtainItem(shopName + "_" + (i - 1));
                    break;
                }
            }
        }

        private void GiveItem(string item)
        {
            MultiWorldMod.Instance.Log("Giving item " + item);
            PlayerData.instance.SetBool("MultiWorldItem." + item, true);
        }
    }
}
