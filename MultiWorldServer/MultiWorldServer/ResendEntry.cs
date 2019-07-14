using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MultiWorldProtocol.Messaging;

namespace MultiWorldServer
{
    class ResendEntry
    {
        public MWMessage Message;
        public DateTime LastSent;

        public ResendEntry(MWMessage message)
        {
            LastSent = DateTime.MinValue;
            Message = message;
        }
    }
}
