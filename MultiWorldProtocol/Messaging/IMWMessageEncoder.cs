using System.IO;
using MultiWorldProtocol.Messaging.Definitions;

namespace MultiWorldProtocol.Messaging
{
    public interface IMWMessageEncoder
    {
        void Encode(BinaryWriter dataStream, IMWMessageProperty definition, MWMessage message);
        void Decode(BinaryReader dataStream, IMWMessageProperty definition, MWMessage message);
    }
}
