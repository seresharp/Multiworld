using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ReconnectMessage)]
    public class MWReconnectMessage : MWMessage
    {
        public MWReconnectMessage()
        {
            MessageType = MWMessageType.ReconnectMessage;
        }
    }

    public class MWReconnectMessageDefinition : MWMessageDefinition<MWReconnectMessage>
    {
        public MWReconnectMessageDefinition() : base(MWMessageType.ReconnectMessage)
        {
        }
    }
}