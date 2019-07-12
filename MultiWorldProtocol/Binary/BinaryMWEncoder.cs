using System;
using System.IO;

public class BinaryMWMessageEncoder : IMWMessageEncoder
{

    public void Encode(BinaryWriter dataStream, IMWMessageProperty property, MWMessage message)
    {
        if (property.Type == typeof(MWMessageType))
        {
            dataStream.Write((int)(MWMessageType)property.GetValue(message));
            return;
        }

        switch (Type.GetTypeCode(property.Type))
        {
            case TypeCode.UInt64:
                dataStream.Write((UInt64)property.GetValue(message));
                break;
            case TypeCode.UInt32:
                dataStream.Write((UInt32)property.GetValue(message));
                break;
            case TypeCode.UInt16:
                dataStream.Write((UInt16)property.GetValue(message));
                break;
            case TypeCode.Byte:
                dataStream.Write((Byte)property.GetValue(message));
                break;
            case TypeCode.Int64:
                dataStream.Write((Int64)property.GetValue(message));
                break;
            case TypeCode.Int32:
                dataStream.Write((Int32)property.GetValue(message));
                break;
            case TypeCode.Int16:
                dataStream.Write((Int16)property.GetValue(message));
                break;
            case TypeCode.String:
                dataStream.Write((String)property.GetValue(message));
                break;
        }
    }

    public void Decode(BinaryReader dataStream, IMWMessageProperty property, MWMessage message)
    {
        object val = null;

        if (property.Type == typeof(MWMessageType))
        {
            val = (MWMessageType)dataStream.ReadInt32();
            property.SetValue(message, val);
            return;
        }

        switch (Type.GetTypeCode(property.Type))
        {
            case TypeCode.UInt64:
                val = dataStream.ReadUInt64();
                break;
            case TypeCode.UInt32:
                val = dataStream.ReadUInt32();
                break;
            case TypeCode.UInt16:
                val = dataStream.ReadUInt16();
                break;
            case TypeCode.Byte:
                val = dataStream.ReadByte();
                break;
            case TypeCode.Int64:
                val = dataStream.ReadInt64();
                break;
            case TypeCode.Int32:
                val = dataStream.ReadInt32();
                break;
            case TypeCode.Int16:
                val = dataStream.ReadInt16();
                break;
            case TypeCode.String:
                val = dataStream.ReadString();
                break;
        }
        property.SetValue(message, val);
    }
}
