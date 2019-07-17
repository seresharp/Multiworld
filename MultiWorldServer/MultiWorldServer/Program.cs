using System;
using System.Diagnostics.Eventing.Reader;

namespace MultiWorldServer
{
    internal class Program
    {
        private static Server Serv;

        private static void Main()
        {
            ServerSettings settings = new ServerSettings();

            Console.WriteLine("Enter number of players");
            settings.Players = int.Parse(Console.ReadLine());
            Console.WriteLine("Enter seed (Leave blank for random)");
            string seedStr = Console.ReadLine();
            if (string.IsNullOrEmpty(seedStr))
            {
                settings.Seed = new Random().Next();
            }
            else if (!int.TryParse(seedStr, out settings.Seed))
            {
                settings.Seed = seedStr.GetHashCode();
            }

            Console.WriteLine("Seed number is " + settings.Seed);

            Console.WriteLine("Shade skips? Y/N");
            settings.ShadeSkips = ParseYN();
            Console.WriteLine("Acid Skips? Y/N");
            settings.AcidSkips = ParseYN();
            Console.WriteLine("Spike Tunnels? Y/N");
            settings.SpikeTunnels = ParseYN();
            Console.WriteLine("Misc Skips? Y/N");
            settings.MiscSkips = ParseYN();
            Console.WriteLine("Fireball Skips? Y/N");
            settings.FireballSkips = ParseYN();

            Serv = new Server(38281, settings);
        }

        private static bool ParseYN()
        {
            string yn = Console.ReadLine();

            if (string.IsNullOrEmpty(yn))
            {
                return false;
            }

            return yn[0] == 'Y' || yn[0] == 'y';
        }
    }
}
