using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldProtocol.Messaging.Definitions
{
    class MWSharedCore : MWMessage
    {
    }

    class MWSharedCoreDefinition : MWMessageDefinition<MWSharedCore>
    {
        public MWSharedCoreDefinition() : base(MWMessageType.SharedCore) { }
    }
}
