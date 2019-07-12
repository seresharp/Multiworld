using System;

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
