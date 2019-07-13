using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

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
