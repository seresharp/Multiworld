using System;
using System.Collections.Generic;

public interface IMWMessageDefinition
{
    MWMessageType MessageType { get; }
    List<IMWMessageProperty> Properties { get; }
}
