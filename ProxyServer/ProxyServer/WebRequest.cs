using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Collections;
using System.Net;
using System.Threading;

namespace ProxyServer
{
    public class WebRequest : WebBase
    {
        public string Method { get; set; }
        public string URI { get; set; }
        public string Version { get; set; }
        public string Hostname { get; set; }
        public int Port { get; set; }

        public WebRequest(NetworkStream s)
        {

            List<string> lines;

            byte[] bodyHead;
            try
            {
                lines = ParseRequest(s, out bodyHead); //parse request into lines
            }
            catch (Exception e) { throw e; }

            string requestLine = lines[0].Replace("\r\n", ""); //request line is the first line
            string[] strings = requestLine.Split(' ');

            if (strings[2] != "HTTP/1.1") //only accept version 1.1
            {
                throw new NotSupportedException(strings[2]);
            }

            Method = strings[0];
            URI = strings[1];
            Version = strings[2];

            string hostname = URI;
            if (hostname.Contains("https://"))
            {
                Port = 443;
                hostname = URI.Replace("https://", "");
            }
            else if (URI.Contains("http://"))
            {
                Port = 80;
                hostname = URI.Replace("http://", "");
            }

            if (hostname.Contains(':'))
            {
                string[] help = hostname.Split(':');
                Hostname = help[0];
                try
                {
                    Port = int.Parse(help[1].Split('/')[0]);
                }
                catch
                {
                    Hostname = hostname.Split('/')[0];
                }
            }
            else
            {
                Hostname = hostname.Split('/')[0];
            }



            Dictionary<string, string> headers = new Dictionary<string, string>(new CWIcompparer()); //create the dictionary using a case insensative comparator

            for (int i = 1; i < lines.Count; i++) //for lines 1 to last add the header fields to a dictionary
            {
                string headerLine = lines[i].Replace("\r\n", ""); //remove linebreaks

                int j = 0;
                while (headerLine[j] != ':')
                {
                    j++;
                    if (j >= headerLine.Length)
                    {
                        throw new IOException();
                    }
                }
                string hKey = headerLine.Substring(0, j);
                string hValue = headerLine.Substring(j + 1);
                headers[hKey] = hValue;
            }

            Headers = headers;
            KeepAlive = false;
            if (headers.ContainsKey("connect") && headers["connect"] == "keep-alive" || headers.ContainsKey("proxy-connection") && headers["proxy-connection"] == "keep-alive")
            {
                KeepAlive = true;
            }
            if (headers.ContainsKey("content-length") && !(headers.ContainsKey("transfer-encoding") && headers["transfer-encoding"].Contains("chunked")))
            {
                BodyStream = new ConcatStream(new MemoryStream(bodyHead), s, int.Parse(headers["content-length"])); //create the instream using the already read bytes and the remainder of the stream
            }
            else
            {
                BodyStream = new ConcatStream(new MemoryStream(bodyHead), s); //create the instream using the already read bytes and the remainder of the stream
            }
        }

        public void MethodNotAllowed()
        {
            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 403 Method Not Found\r\nAllow : GET, HEAD, PUT\r\n\r\n");
            BodyStream.Write(response, 0, response.Length);
        }
    }
}
