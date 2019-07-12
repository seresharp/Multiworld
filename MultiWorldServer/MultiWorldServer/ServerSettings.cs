using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldServer
{
    public struct ServerSettings
    {
        public ServerSettings(bool a)
        {
            ShadeSkips = true;
            AcidSkips = true;
            SpikeTunnels = true;
            MiscSkips = true;
            FireballSkips = true;
            MagSkips = true;

            NoClaw = false;
            Players = 5;
            Seed = new Random().Next();
        }

        public int Seed;
        public int Players;
        public bool ShadeSkips;
        public bool AcidSkips;
        public bool SpikeTunnels;
        public bool MiscSkips;
        public bool FireballSkips;
        public bool MagSkips;
        public bool NoClaw;
    }
}
