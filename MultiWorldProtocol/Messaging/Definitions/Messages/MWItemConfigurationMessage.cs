using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ItemConfigurationMessage)]
    public class MWItemConfigurationMessage : MWMessage
    {
        public string Location { get; set; }
        public string Item { get; set; }
        public UInt16 PlayerId { get; set; }
        public MWItemConfigurationMessage()
        {
            MessageType = MWMessageType.ItemConfigurationMessage;
        }
    }

    public class MWItemConfigurationMessageDefinition : MWMessageDefinition<MWItemConfigurationMessage>
    {
        public MWItemConfigurationMessageDefinition() : base(MWMessageType.ItemConfigurationMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWItemConfigurationMessage>(nameof(MWItemConfigurationMessage.Location)));
            Properties.Add(new MWMessageProperty<string, MWItemConfigurationMessage>(nameof(MWItemConfigurationMessage.Item)));
            Properties.Add(new MWMessageProperty<UInt16, MWItemConfigurationMessage>(nameof(MWItemConfigurationMessage.PlayerId)));
        }
    }
}