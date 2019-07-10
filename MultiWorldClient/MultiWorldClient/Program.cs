using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MultiWorldClient
{
    internal class Program
    {
        private static readonly object InputLock = new object();
        private static string _input;

        private static TcpClient _client;
        private static readonly Stopwatch Watch = new Stopwatch();

        private static void Main()
        {
            new Thread(ReadLines).Start();

            string ip = null;
            int? port = null;

            while (true)
            {
                if ((_client == null || !_client.Connected) && ip != null && port != null)
                {
                    Console.Write("Attempting to connect to server... ");
                    try
                    {
                        _client = new TcpClient
                        {
                            ReceiveTimeout = 2000,
                            SendTimeout = 2000
                        };

                        _client.Connect(ip, port.Value);
                        Console.WriteLine("Success!");

                        Watch.Reset();
                        Watch.Start();
                    }
                    catch
                    {
                        Console.WriteLine("Failed");

                        // Don't kill the CPU
                        Thread.Sleep(1000);
                        continue;
                    }
                }

                // Check for user messages
                string msg;
                lock (InputLock)
                {
                    msg = _input;
                    _input = null;
                }

                // Check for IP and port
                if (_client == null || !_client.Connected)
                {
                    if (!string.IsNullOrEmpty(msg))
                    {
                        string[] msgArgs = msg.Split(' ');

                        switch (msgArgs[0])
                        {
                            case "IP" when msgArgs.Length == 2:
                                ip = msgArgs[1];
                                break;
                            case "PORT" when msgArgs.Length == 2:
                                if (int.TryParse(msgArgs[1], out int i))
                                {
                                    port = i;
                                }

                                break;
                        }
                    }

                    continue;
                }

                if (!string.IsNullOrEmpty(msg))
                {
                    SendMessage(msg);
                }

                // Send ping messages periodically
                if (Watch.Elapsed.TotalSeconds >= 10)
                {
                    SendMessage("PING");

                    Watch.Reset();
                    Watch.Start();
                }

                // Read data from server
                if (_client.Available > 0)
                {
                    byte[] buf = new byte[_client.Available];

                    try
                    {
                        NetworkStream stream = _client.GetStream();
                        stream.BeginRead(buf, 0, _client.Available, ReadFromServer, (stream, buf));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to read data from server:\n{e}");
                        _client.Close();
                        _client = new TcpClient();
                    }
                }

                // Don't kill the CPU
                Thread.Sleep(10);
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private static void SendMessage(string msg)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(msg);

            try
            {
                NetworkStream stream = _client.GetStream();
                stream.BeginWrite(bytes, 0, bytes.Length, WriteToServer, stream);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send message '{msg}' to server:\n{e}");
            }
        }

        private static void WriteToServer(IAsyncResult res)
        {
            NetworkStream stream = (NetworkStream)res.AsyncState;
            stream.EndWrite(res);
        }

        private static void ReadFromServer(IAsyncResult res)
        {
            (NetworkStream stream, byte[] buf) = ((NetworkStream, byte[]))res.AsyncState;
            stream.EndRead(res);

            string message = Encoding.ASCII.GetString(buf);
            if (message == "PING")
            {
                return;
            }

            Console.WriteLine($"Server: {message}");
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
