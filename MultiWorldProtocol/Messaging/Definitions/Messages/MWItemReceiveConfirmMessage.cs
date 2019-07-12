using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ItemReceiveConfirmMessage)]
    public class MWItemReceiveConfirmMessage : MWMessage
    {
        public string Item { get; set; }
        public string From { get; set; }

        public MWItemReceiveConfirmMessage()
        {
            MessageType = MWMessageType.ItemReceiveConfirmMessage;
        }
    }

    public class MWItemReceiveConfirmDefinition : MWMessageDefinition<MWItemReceiveConfirmMessage>
    {
        public MWItemReceiveConfirmDefinition() : base(MWMessageType.ItemReceiveConfirmMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWItemReceiveConfirmMessage>(nameof(MWItemReceiveConfirmMessage.Item)));
            Properties.Add(new MWMessageProperty<string, MWItemReceiveConfirmMessage>(nameof(MWItemReceiveConfirmMessage.From)));
        }
    }
}