using System;
using System.Collections.Generic;

public class MWMessageDefinition<T> where T:MWMessage
{
    List<IMWMessageProperty> Properties = new List<IMWMessageProperty>();

    public MWMessageDefinition()
	{
        Properties.Add(new MWMessageProperty<MWMessageType, T>("MessageType"));
        Properties.Add(new MWMessageProperty<ulong, T>("SenderUid"));
        Properties.Add(new MWMessageProperty<ulong, T>("MessageId"));
	}
}
