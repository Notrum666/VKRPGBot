using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VKRPGBot
{
    class Program
    {
        public static Random rng = new Random();
        private static string token = "fcd3aba83673c2deddee711edf719275f5af7f58278ce23bffb81472f61da5b529f0666e4cc3960a46f0a";
        private static ulong groupID = 195853487;
        private static Server server;
        private static Game game;
        static void Main(string[] args)
        {
            Translator.Load("en_us");
            game = new Game();
            server = new Server();
            server.start(token, groupID);
        }
    }
}
