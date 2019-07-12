using System;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions;

[MWMessageType(MWMessageType.DisconnectMessage)]
public class MWDisconnectMessage : MWMessage
{
	public MWDisconnectMessage()
	{
	}
}

public class MWDisconnectMessageDefinition : MWMessageDefinition<MWDisconnectMessage>
{
    public MWDisconnectMessageDefinition() : base(MWMessageType.DisconnectMessage)
    {
    }
}
