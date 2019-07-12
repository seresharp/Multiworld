using System;
using System.Collections.Generic;

public class MWMessageDefinition<T> : IMWMessageDefinition where T : MWMessage
{
    public List<IMWMessageProperty> Properties { private set; get; }
    public MWMessageType MessageType { get; private set; }

    public MWMessageDefinition(MWMessageType type)
    {
        Properties = new List<IMWMessageProperty>();
        MessageType = type;
        Properties.Add(new MWMessageProperty<MWMessageType, T>("MessageType"));
        Properties.Add(new MWMessageProperty<ulong, T>("SenderUid"));
        Properties.Add(new MWMessageProperty<ulong, T>("MessageId"));
    }
}
