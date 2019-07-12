using System;
using System.IO;

public struct MWPackedMessage
{
    public uint Length;
    public byte[] Buffer;

    /// <summary>
    /// Create with length and buffer. Does not perform sanity check on length and buffer
    /// </summary>
    /// <param name="length"></param>
    /// <param name="buffer"></param>
    public MWPackedMessage(uint length, byte[] buffer)
    {
        Length = length;
        Buffer = buffer;
    }

    /// <summary>
    /// Create from buffer. Will parse the length out of the buffer and sanity check
    /// </summary>
    /// <param name="buffer"></param>
    public MWPackedMessage(byte[] buffer)
    {
        Buffer = buffer;
        Length = BitConverter.ToUInt32(buffer, 0);

        if(Buffer.Length != Length)
        {
            throw new InvalidDataException("Buffer Length and length in data are mismatched");
        }
    }
}
