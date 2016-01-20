using System;
using System.Net;
using System.Net.Sockets;

namespace Simple
{
    public class PortForward
    {
        private const string Usage = @"
PortForward.exe simply forwards connections from a local IPv4 TCP port to
another until you terminate it with Ctrl+C.

Usage:   PortForward [--help] <fromPort> <toPort>

For example, to forwards TCP connections from port 1111 to localhost:52527
use:
         PortForward 1111 52527
";

        static void Main(string[] args)
        {
            bool ok = true;
            int pos;
            for (pos = 0; pos < args.Length && args[pos].StartsWith("-"); pos++)
            {
                var arg = args[pos].TrimStart('-');
                switch (arg)
                {
                    case "?":
                    case "help":
                        ok = false;
                        break;

                    default:
                        Console.WriteLine("ERROR: Unrecognised option: " + args[pos]);
                        ok = false;
                        break;
                }
            }

            if ((args.Length - pos) != 2)
            {
                ok = false;
            }

            int fromPort = 0;
            int toPort = 0;
            if (ok)
            {
                try
                {
                    fromPort = int.Parse(args[pos]);
                    toPort = int.Parse(args[pos + 1]);
                }
                catch (ArgumentException)
                {
                    ok = false;
                }
            }

            if (!ok)
            {
                Console.WriteLine(Usage);
                return;
            }

            Forward(
                new IPEndPoint(IPAddress.Any, fromPort),
                new IPEndPoint(IPAddress.Loopback, toPort));
        }

        public static void Forward(IPEndPoint from, IPEndPoint to)
        {
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            serverSocket.Bind(from);
            serverSocket.Listen(99);

            for (;;)
            {
                var source = serverSocket.Accept();

                var destination = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                destination.Connect(to);

                // TODO: Probably should keep hold of references to these until completed
                new Forwarder(source, destination);
                new Forwarder(destination, source);
            }
        }

        private class Forwarder
        {
            public Socket Source { get; set; }
            public Socket Destination { get; set; }
            public byte[] Data { get; set; }

            public Forwarder(Socket src, Socket dst)
            {
                Source = src;
                Destination = dst;
                Data = new byte[8192];

                Source.BeginReceive(Data, 0, Data.Length, 0, OnDataReceive, this);
            }

            private void OnDataReceive(IAsyncResult result)
            {
                // Don't need result.AsyncState
                try
                {
                    var count = Source.EndReceive(result);
                    if (count > 0)
                    {
                        Destination.Send(Data, count, SocketFlags.None);
                        Source.BeginReceive(Data, 0, Data.Length, 0, OnDataReceive, this);
                    }
                }
                catch
                {
                    Destination.Close();
                    Source.Close();
                }
            }

        }
    }
}
