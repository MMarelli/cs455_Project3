using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;

namespace ProxyServer
{
    public static class ProxyServer
    {
        static TcpListener _Listener;
        static bool _Debug = false;

        public static void Start(int port)
        {
            _Listener = new TcpListener(Dns.GetHostEntry("localhost").AddressList[0], port);
            _Listener.Start();
            while (true)
            {
                TcpClient client = _Listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(o => ClientRunner(client));
            }
        }

        /// <summary>
        /// Runners job is to proxy the request
        /// </summary>
        /// <param name="client"></param>
        private static void ClientRunner(TcpClient client)
        {
            NetworkStream clientStream = client.GetStream();
            WebRequest clientRequest;
            try
            {
                clientRequest = new WebRequest(clientStream);
            }
            catch (Exception e)
            {
                if (_Debug)
                {
                    Console.WriteLine("{0} - {1}", e, e.Message);
                }
                client.Close();
                return;
            }
            TcpClient server;
            try
            {
                if (_Debug)
                {
                    Console.WriteLine("connecting to - {0}:{1}", clientRequest.Hostname, clientRequest.Port);
                }
                server = new TcpClient(clientRequest.Hostname, clientRequest.Port);
            }
            catch (Exception e)
            {
                client.Close();
                if (_Debug)
                {
                    Console.WriteLine("{0} - {1}", e, e.Message);
                }
                return;
            }

            NetworkStream serverStream = server.GetStream();
            while (server.Connected && client.Connected)
            {
                if (_Debug)
                {
                    Console.WriteLine("client sending request to {0}", clientRequest.Hostname);
                }
                Console.WriteLine("Requtest Header:\r\n{0}", clientRequest.Header);
                if (clientRequest.Method == "connect")
                {
                    clientRequest.MethodNotAllowed();
                    break;
                }
                else
                {
                    try
                    {
                        clientRequest.Forward(serverStream);
                        WebResponse serverResponse;
                        while (!serverStream.DataAvailable && server.Connected && client.Connected) { } //wait until there is somthin available to read
                        serverResponse = new WebResponse(serverStream);
                        Console.WriteLine("Response Header:\r\n{0}", serverResponse.Header);

                        serverResponse.Forward(clientStream);

                        if (!serverResponse.KeepAlive || !clientRequest.KeepAlive)
                        {
                            client.Close();
                            server.Close();
                        }
                        else
                        {
                            while (!clientStream.DataAvailable && client.Connected && server.Connected) { } //wait until there is somthin available to read
                            clientRequest = new WebRequest(clientStream);
                        }

                    }
                    catch (Exception e)
                    {
                        if (_Debug)
                        {
                            Console.WriteLine("{0} - {1}", e, e.Message);
                        }
                        break;
                    }
                }
            }
            server.Close();
            client.Close();
        }
    }
}
