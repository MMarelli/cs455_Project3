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

        public static void Start(int port)
        {
            _Listener = new TcpListener(Dns.GetHostEntry("localhost").AddressList[0], port);
            _Listener.Start();
            while (true)
            {
                TcpClient client = _Listener.AcceptTcpClient();
                Thread runner = new Thread(() => ClientRunner(client));
                runner.Start();
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
                Console.WriteLine("{0} - {1}", e, e.Message);
                client.Close();
                return;
            }
            TcpClient host;
            try
            {
                Console.WriteLine("connecting to - {0}:{1}", clientRequest.Hostname, clientRequest.Port);
                host = new TcpClient(clientRequest.Hostname, clientRequest.Port);
            }
            catch (SocketException e)
            {
                client.Close();
                Console.WriteLine("{0} - {1}", e, e.Message);
                return;
            }

            NetworkStream hostStream = host.GetStream();
            while (host.Connected && client.Connected)
            {
                Console.WriteLine("client sending request to {0}", clientRequest.Hostname);
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
                        clientRequest.Forward(hostStream);
                        WebResponse hostResponse;
                        while (!hostStream.DataAvailable && host.Connected && client.Connected) { } //wait until there is somthin available to read
                        hostResponse = new WebResponse(hostStream);
                        Console.WriteLine("Response Header:\r\n{0}", hostResponse.Header);

                        hostResponse.Forward(clientStream);

                        if (!hostResponse.KeepAlive || !clientRequest.KeepAlive)
                        {
                            client.Close();
                            host.Close();
                        }
                        else
                        {
                            while (!clientStream.DataAvailable && client.Connected && host.Connected) { } //wait until there is somthin available to read
                            clientRequest = new WebRequest(clientStream);
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("{0} - {1}", e, e.Message);
                        break;
                    }
                }
            }
            host.Close();
            client.Close();
        }
    }
}
