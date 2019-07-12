using System;

public enum MWMessageType
{
    InvalidMessage=0,
    SharedCore=1,
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
