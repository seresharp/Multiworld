using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions.Messages;

namespace MultiWorldServer
{
    class Client
    {
        public ulong UID;
        public TcpClient TcpClient;
        public bool FullyConnected;

        public Session Session;
    }
}
