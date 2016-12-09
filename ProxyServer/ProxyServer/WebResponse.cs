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
    public class WebResponse : WebBase
    {
        public string StatusLine { get; set; }

        public WebResponse(NetworkStream s)
        {
            List<string> lines;

            byte[] bodyHead;
            try
            {
                lines = ParseRequest(s, out bodyHead); //parse request into lines
            }
            catch (Exception e) { throw e; }

            StatusLine = lines[0].Replace("\r\n", ""); //status line is the first line

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); //create the dictionary using a case insensative comparator

            for (int i = 1; i < lines.Count; i++) //for lines 1 to last-1 add the header fields to a dictionary
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
    }
}
