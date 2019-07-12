using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldProtocol.Messaging.Definitions
{
    [MWMessageType(MWMessageType.SharedCore)]
    public class MWSharedCore : MWMessage
    {
        public MWSharedCore()
        {
            MessageType = MWMessageType.SharedCore;
        }
    }

    public class MWSharedCoreDefinition : MWMessageDefinition<MWSharedCore>
    {
        public MWSharedCoreDefinition() : base(MWMessageType.SharedCore) { }
    }
}
