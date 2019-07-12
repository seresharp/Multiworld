using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ItemConfigurationConfirmMessage)]
    public class MWItemConfigurationConfirmMessage : MWMessage
    {
        public string Location { get; set; }
        public string Item { get; set; }
        public UInt16 PlayerId { get; set; }
        public MWItemConfigurationConfirmMessage()
        {
            MessageType = MWMessageType.ItemConfigurationConfirmMessage;
        }
    }

    public class MWItemConfigurationConfirmMessageDefinition : MWMessageDefinition<MWItemConfigurationConfirmMessage>
    {
        public MWItemConfigurationConfirmMessageDefinition() : base(MWMessageType.ItemConfigurationConfirmMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWItemConfigurationConfirmMessage>(nameof(MWItemConfigurationConfirmMessage.Location)));
            Properties.Add(new MWMessageProperty<string, MWItemConfigurationConfirmMessage>(nameof(MWItemConfigurationConfirmMessage.Item)));
            Properties.Add(new MWMessageProperty<UInt16, MWItemConfigurationConfirmMessage>(nameof(MWItemConfigurationConfirmMessage.PlayerId)));
        }
    }
}