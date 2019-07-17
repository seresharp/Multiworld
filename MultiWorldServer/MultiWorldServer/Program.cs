using System;
using System.Diagnostics.Eventing.Reader;
using System.Threading;

namespace MultiWorldServer
{
    internal class Program
    {
        private static Server Serv;

        private static void Main()
        {
            Console.WriteLine("Enter number of players");
            int players = int.Parse(Console.ReadLine());
            Console.WriteLine("Enter seed (Leave blank for random)");
            string seedStr = Console.ReadLine();
            int seed;
            if (string.IsNullOrEmpty(seedStr))
            {
                seed = new Random().Next();
            }
            else if (!int.TryParse(seedStr, out seed))
            {
                seed = seedStr.GetHashCode();
            }

            Console.WriteLine("Seed number is " + seed);

            Serv = new Server(38281, new ServerSettings {Seed = seed, Players = players});

            while(Serv.Running)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
