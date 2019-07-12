using System;

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
