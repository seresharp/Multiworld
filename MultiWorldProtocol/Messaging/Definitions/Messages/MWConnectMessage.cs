using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.ConnectMessage)]
    public class MWConnectMessage : MWMessage
    {
        public MWConnectMessage()
        {
        }
    }

    public class MWConnectMessageDefinition : MWMessageDefinition<MWConnectMessage>
    {
        public MWConnectMessageDefinition() : base(MWMessageType.ConnectMessage)
        {
        }
    }
}