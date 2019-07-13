using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldClient
{
    struct PlayerItem
    {
        public string Item;
        public uint PlayerId;

        public PlayerItem(string item, uint playerId)
        {
            Item = item;
            PlayerId = playerId;
        }
    }
}
