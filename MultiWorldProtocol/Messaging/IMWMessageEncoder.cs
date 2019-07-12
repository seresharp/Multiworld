using System;
using System.IO;

public interface IMWMessageEncoder
{
    void Encode(BinaryWriter dataStream, IMWMessageProperty definition, MWMessage message);
    void Decode(BinaryReader dataStream, IMWMessageProperty definition, MWMessage message);
}
