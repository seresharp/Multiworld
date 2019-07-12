using System.Collections.Generic;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging
{
    public interface IMWMessageDefinition
    {
        MWMessageType MessageType { get; }
        List<IMWMessageProperty> Properties { get; }
    }
}
