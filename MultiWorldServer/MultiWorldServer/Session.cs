using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions.Messages;

namespace MultiWorldServer
{
    class Session
    {

        private static readonly Random Rnd = new Random();
        public string Name;
        public string Token;
        public ushort PID;

        public readonly List<ResendEntry> MessagesToConfirm = new List<ResendEntry>();
        public readonly HashSet<string> PickedUpLocations = new HashSet<string>();

        public Session(string Name)
        {
            Token = GenerateToken();
            this.Name = Name;
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

        public void QueueConfirmableMessage(MWMessage message)
        {
            if (message.MessageType != MWMessageType.ItemConfigurationMessage && message.MessageType != MWMessageType.ItemReceiveMessage)
            {
                throw new InvalidOperationException("Server should only queue ItemConfiguration and ItemReceive messages for confirmation");
            }
            lock (MessagesToConfirm)
            {
                MessagesToConfirm.Add(new ResendEntry(message));
            }
        }

        public void ConfirmMessage(MWMessage message)
        {
            if (message.MessageType == MWMessageType.ItemConfigurationConfirmMessage)
            {
                ConfirmItemConfiguration((MWItemConfigurationConfirmMessage)message);
            }
            else if (message.MessageType == MWMessageType.ItemReceiveConfirmMessage)
            {
                ConfirmItemReceive((MWItemReceiveConfirmMessage)message);
            }
            else
            {
                throw new InvalidOperationException("Must only confirm ItemConfiguration and ItemReceive messages.");
            }
        }

        private void ConfirmItemConfiguration(MWItemConfigurationConfirmMessage message)
        {
            lock (MessagesToConfirm)
            {
                for (int i = MessagesToConfirm.Count - 1; i >= 0; i--)
                {
                    MWItemConfigurationMessage icm = MessagesToConfirm[i].Message as MWItemConfigurationMessage;
                    if (icm.Item == message.Item && icm.PlayerId == message.PlayerId)
                    {
                        MessagesToConfirm.RemoveAt(i);
                    }
                }
            }
        }

        private void ConfirmItemReceive(MWItemReceiveConfirmMessage message)
        {
            lock (MessagesToConfirm)
            {
                for (int i = MessagesToConfirm.Count - 1; i >= 0; i--)
                {
                    MWItemReceiveMessage icm = MessagesToConfirm[i].Message as MWItemReceiveMessage;
                    if (icm.Item == message.Item && icm.From == message.From)
                    {
                        MessagesToConfirm.RemoveAt(i);
                    }
                }
            }
        }
    }
}
