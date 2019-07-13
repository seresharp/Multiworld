namespace MultiWorldServer
{
    internal class Program
    {
        private static Server Serv;

        private static void Main()
        {
            Serv = new Server(38281);
        }
    }
}
