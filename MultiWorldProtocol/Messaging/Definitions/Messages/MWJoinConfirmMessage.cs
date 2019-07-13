using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging.Definitions.Messages
{
    [MWMessageType(MWMessageType.JoinConfirmMessage)]
    public class MWJoinConfirmMessage : MWMessage
    {
        public string DisplayName { get; set; }
        public string Token { get; set; }
        public UInt16 PlayerId { get; set; }

        public MWJoinConfirmMessage()
        {
            MessageType = MWMessageType.JoinConfirmMessage;
        }
    }

    public class MWJoinConfirmMessageDefinition : MWMessageDefinition<MWJoinConfirmMessage>
    {
        public MWJoinConfirmMessageDefinition() : base(MWMessageType.JoinConfirmMessage)
        {
            Properties.Add(new MWMessageProperty<string, MWJoinConfirmMessage>(nameof(MWJoinConfirmMessage.DisplayName)));
            Properties.Add(new MWMessageProperty<string, MWJoinConfirmMessage>(nameof(MWJoinConfirmMessage.Token)));
            Properties.Add(new MWMessageProperty<UInt16, MWJoinConfirmMessage>(nameof(MWJoinConfirmMessage.PlayerId)));
        }
    }
}