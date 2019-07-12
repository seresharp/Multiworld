using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ItemSendConfirmMessage)]
    public class MWItemSendConfirmMessage : MWMessage
    {
        public string Item { get; set; }
        public string To { get; set; }

        public MWItemSendConfirmMessage()
        {
            MessageType = MWMessageType.ItemSendConfirmMessage;
        }
    }

    public class MWItemSendConfirmDefinition : MWMessageDefinition<MWItemSendConfirmMessage>
    {
        public MWItemSendConfirmDefinition() : base(MWMessageType.ItemSendConfirmMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWItemSendConfirmMessage>(nameof(MWItemSendConfirmMessage.Item)));
            Properties.Add(new MWMessageProperty<string, MWItemSendConfirmMessage>(nameof(MWItemSendConfirmMessage.To)));
        }
    }
}