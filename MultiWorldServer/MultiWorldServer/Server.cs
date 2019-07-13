using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiWorldProtocol.Messaging;
using MultiWorldProtocol.Binary;
using System.Net.Sockets;
using System.Net;

namespace MultiWorldServer
{
    class Server
    {
        private ulong nextUID = 1;
        private ushort nextPID = 1;
        private readonly MWMessagePacker Packer = new MWMessagePacker(new BinaryMWMessageEncoder());
        private readonly List<Client> Unidentified = new List<Client>();


        private readonly Dictionary<ulong, Client> Clients = new Dictionary<ulong, Client>();
        private readonly Dictionary<string, Session> Sessions = new Dictionary<string, Session>();
        private TcpListener _server;

        public Server(int port)
        {
            //Listen on any ip
            _server = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
            _server.Start();

            Console.WriteLine("Server started!");

            _server.BeginAcceptTcpClient(AcceptClient, _server);
        }

        private void AcceptClient(IAsyncResult res)
        {
            Client client = new Client
            {
                TcpClient = _server.EndAcceptTcpClient(res)
            };

            _server.BeginAcceptTcpClient(AcceptClient, _server);

            if (!client.TcpClient.Connected)
            {
                return;
            }

            client.TcpClient.ReceiveTimeout = 2000;
            client.TcpClient.SendTimeout = 2000;

            lock (Unidentified)
            {
                Unidentified.Add(client);
            }
        }

        private bool SendMessage(MWMessage message, Client client)
        {
            if (client?.TcpClient == null || !client.TcpClient.Connected)
            {
                return false;
            }

            try
            {
                byte[] bytes = Packer.Pack(message).Buffer;

                NetworkStream stream = client.TcpClient.GetStream();
                stream.BeginWrite(bytes, 0, bytes.Length, WriteToClient, stream);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send message to '{client.Session?.Name}':\n{e}");
                return false;
            }
        }

        private static void WriteToClient(IAsyncResult res)
        {
            NetworkStream stream = (NetworkStream)res.AsyncState;
            stream.EndWrite(res);
        }

        private Client GetClient(ulong uuid)
        {
            return Clients.TryGetValue(uuid, out Client client) ? client : null;
        }
    }
}
