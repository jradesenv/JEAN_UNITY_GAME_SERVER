using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace test_unity_udp_csharp_server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GameServer server = new GameServer();
            server.Start();
        }
    }
}
