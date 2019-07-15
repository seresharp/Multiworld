using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ItemConfigurationRequestMessage)]
    public class MWItemConfigurationRequestMessage : MWMessage
    {
        public MWItemConfigurationRequestMessage()
        {
            MessageType = MWMessageType.ItemConfigurationRequestMessage;
        }
    }

    public class MWItemConfigurationRequestMessageDefinition : MWMessageDefinition<MWItemConfigurationRequestMessage>
    {
        public MWItemConfigurationRequestMessageDefinition() : base(MWMessageType.ItemConfigurationRequestMessage)
        {
        }
    }
}