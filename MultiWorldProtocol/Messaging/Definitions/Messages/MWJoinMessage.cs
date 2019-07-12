using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.JoinMessage)]
    public class MWJoinMessage : MWMessage
    {
        public string DisplayName { get; set; }
        public string Token { get; set; }

        public MWJoinMessage()
        {
            MessageType = MWMessageType.JoinMessage;
        }
    }

    public class MWJoinMessageDefinition : MWMessageDefinition<MWJoinMessage>
    {
        public MWJoinMessageDefinition() : base(MWMessageType.JoinMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWJoinMessage>(nameof(MWJoinMessage.DisplayName)));
            Properties.Add(new MWMessageProperty<string, MWJoinMessage>(nameof(MWJoinMessage.Token)));
        }
    }
}