using System;
using System.IO;

public class MWPackedMessage
{
    ulong length;
    ulong msgId;
    MWMessageType msgType;
    byte buffer;

    public MWPackedMessage(BinaryReader stream)
    {
        ulong length = stream.ReadUInt64();
        ulong msgId = stream.ReadUInt64();
        uint msgType = stream.ReadUInt32();

    }
}
