using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldServer
{
    class Session
    {

        private static readonly Random Rnd = new Random();
        public string Name;
        public string Token;
        public ushort PID;

        public Session(string Name)
        {
            Token = GenerateToken();
            this.Name = Name;
        }

        private static string GenerateToken()
        {
            byte[] bytes = new byte[16];

            for (int i = 0; i < 16; i++)
            {
                bytes[i] = (byte)Rnd.Next(33, 126);
            }

            return Encoding.ASCII.GetString(bytes);
        }
    }
}
