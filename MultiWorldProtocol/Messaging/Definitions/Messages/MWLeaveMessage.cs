using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.LeaveMessage)]
    public class MWLeaveMessage : MWMessage
    {
        public string Token { get; set; }

        public MWLeaveMessage()
        {
            MessageType = MWMessageType.LeaveMessage;
        }
    }

    public class MWLeaveMessageDefinition : MWMessageDefinition<MWLeaveMessage>
    {
        public MWLeaveMessageDefinition() : base(MWMessageType.LeaveMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWLeaveMessage>(nameof(MWLeaveMessage.Token)));
        }
    }
}