using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using MultiWorldProtocol.Binary;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Messaging.Definitions.Messages;

namespace MultiWorldClient
{
    internal class Program
    {
        private static ClientConnection connection;
        private static readonly object InputLock = new object();
        private static string _input;

        private static void Main()
        {

            Console.WriteLine("Enter IP and port in the form '127.0.0.1:5001'");

            string[] input = Console.ReadLine().Split(':');

            Console.WriteLine("Please enter a Username");

            string Username = Console.ReadLine();

            connection = new ClientConnection(input[0], int.Parse(input[1]), Username);

            new Thread(ReadLines).Start();

            while (true)
            {

                // Check for user messages
                string msg;
                lock (InputLock)
                {
                    msg = _input;
                    _input = null;
                }

                if (!string.IsNullOrEmpty(msg))
                {
                    connection.Say(msg);
                }

                // Don't kill the CPU
                Thread.Sleep(10);
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private static void ReadLines()
        {
            while (true)
            {
                string str = Console.ReadLine();

                lock (InputLock)
                {
                    _input = str;
                }
            }

            // ReSharper disable once FunctionNeverReturns
        }
    }
}
