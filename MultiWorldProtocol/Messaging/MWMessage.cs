using System;

public class MWMessage
{
    long senderUid;
    long messageId;
    MWMessageType messageType;
	public MWMessage()
	{
	}

    public long SenderUid { get => senderUid; set => senderUid = value; }
    public long MessageId { get => messageId; set => messageId = value; }
    public MWMessageType MessageType { get => messageType; set => messageType = value; }
}
