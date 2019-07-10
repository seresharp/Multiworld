using System;

public enum MWMessageType
{
    InvalidMessage=0,
    ConnectMessage,
    ReconnectMessage,
    DisconnectMessage,
    JoinMessage,
    JoinConfirmMessage,
    LeaveMessage,
    ItemConfigurationMessage,
    ItemConfigurationConfirmMessage,
    ItemReceiveMessage,
    ItemReceiveConfirmMessage,
    ItemSendMessage,
    ItemSendConfirmMessage,
    ItemNotifyMessage,
}
