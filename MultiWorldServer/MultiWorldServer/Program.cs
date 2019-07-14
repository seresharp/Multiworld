namespace MultiWorldServer
{
    internal class Program
    {
        private static Server Serv;

        private static void Main()
        {
            Serv = new Server(38281, new ServerSettings {Seed = 12345, Players = 1});
        }
    }
}
