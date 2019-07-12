using System;

[MWMessageType(MWMessageType.ConnectMessage)]
public class MWConnectMessage : MWMessage
{
    public string HelloMessage { get; set; }
	public MWConnectMessage()
	{
	}
}

public class MWConnectMessageDefinition : MWMessageDefinition<MWConnectMessage>
{
    public MWConnectMessageDefinition() : base(MWMessageType.ConnectMessage)
    {
        this.Properties.Add(new MWMessageProperty<string, MWConnectMessage>("HelloMessage"));
    }
}
