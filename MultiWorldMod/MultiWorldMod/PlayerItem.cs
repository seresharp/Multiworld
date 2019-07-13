using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldMod
{
    public struct PlayerItem
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
