using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ItemSendMessage)]
    public class MWItemSendMessage : MWMessage
    {
        public string Item { get; set; }
        public string To { get; set; }

        public MWItemSendMessage()
        {
            MessageType = MWMessageType.ItemSendMessage;
        }
    }

    public class MWItemSendDefinition : MWMessageDefinition<MWItemSendMessage>
    {
        public MWItemSendDefinition() : base(MWMessageType.ItemSendMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWItemSendMessage>(nameof(MWItemSendMessage.Item)));
            Properties.Add(new MWMessageProperty<string, MWItemSendMessage>(nameof(MWItemSendMessage.To)));
        }
    }
}