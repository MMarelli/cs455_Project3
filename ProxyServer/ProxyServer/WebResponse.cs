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
    public class WebResponse
    {
        public string StatusLine { get; set; }
        public string Header { get; set; } //unmodified header
        public Dictionary<string, string> Headers { get; set; }
        public Stream BodyStream { get; set; }
        public bool KeepAlive { get; set; }

        public WebResponse(NetworkStream s)
        {
            List<string> lines;

            try
            {
                lines = ParseRequest(s); //parse request into lines
            }
            catch (Exception e) { throw e; }

            StatusLine = lines[0].Replace("\r\n", ""); //status line is the first line

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); //create the dictionary using a case insensative comparator

            for (int i = 1; i < lines.Count - 1; i++) //for lines 1 to last-1 add the header fields to a dictionary
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
            if (headers.ContainsKey("connect"))
            {
                if (headers["connect"] == "keep-alive")
                {
                    if (headers.ContainsKey("content-length") || headers.ContainsKey("transfer-coding"))
                    {
                        KeepAlive = true;
                    }
                }
            }
            if (headers.ContainsKey("content-length"))
            {
                BodyStream = new ConcatStream(new MemoryStream(Encoding.ASCII.GetBytes(lines.Last())), s, int.Parse(headers["content-length"])); //create the instream using the already read bytes and the remainder of the stream
            }
            else
            {
                BodyStream = new ConcatStream(new MemoryStream(Encoding.ASCII.GetBytes(lines.Last())), s); //create the instream using the already read bytes and the remainder of the stream
            }
        }

        private List<string> ParseRequest(NetworkStream s)
        {
            List<string> lines = new List<string>();
            StringBuilder header = new StringBuilder();

            byte[] buffer = new byte[2048];
            StringBuilder builder = new StringBuilder();

            bool EOH = false; //strop reading once end of header

            int bRead = s.Read(buffer, 0, 2048); //the bytes read from stream Timeout throws exception

            while (true) //read in bytes until end of header 
            {
                for (int i = 0; i < bRead; i++)
                {
                    builder.Append((char)buffer[i]);
                    header.Append((char)buffer[i]);
                    if (builder.ToString().Contains("\r\n"))
                    {
                        if (builder.ToString() == "\r\n" && lines.Last().Contains("\r\n")) //we've reached the end of header
                        {
                            EOH = true;
                            builder.Clear(); //clear last line break
                            i++; //increment to start of body
                            
                            while (i < bRead) //read the start of the body in
                            {
                                builder.Append(buffer[i]);
                                i++;
                            }
                            lines.Add(builder.ToString()); //and the start of the body to the end of the request.
                            break;
                        }
                        lines.Add(builder.ToString());
                        builder.Clear();
                    }
                }
                if (EOH || bRead == 0)
                {
                    break;
                }
                else
                {
                    bRead = s.Read(buffer, 0, 2048);
                }
            }

            if (!EOH)  //verify end of header reached
            {
                throw new IOException();
            }

            Header = header.ToString();
            return lines;
        }


        public void Forward(NetworkStream ns) //posibly need to return a byte array if part of the next message was read to concatingate the stream with the network stream
        {
            if (Headers.ContainsKey("transfer-coding"))
            {
                byte[] header = Encoding.ASCII.GetBytes(Header);
                ns.Write(header, 0, header.Length);

                while (true) //while we are still using the current message
                {
                    //now read the next byte until CRLF and remove CRLF this value is x where x is a hex representation of lenth.
                    string x;
                    byte[] buf = new byte[3];
                    BodyStream.Read(buf, 0, 3);
                    x = Encoding.ASCII.GetString(buf);
                    while (!x.Contains("\r\n"))
                    {
                        if (x.Contains("\r"))
                        {
                            BodyStream.Read(buf, 0, 1);
                            x += (char)buf[0];
                        }
                        else
                        {
                            BodyStream.Read(buf, 0, 2);
                            x += (char)buf[0];
                            x += (char)buf[1];
                        }
                        x = x.Replace("\r\n", "");
                    }
                    int length = Convert.ToInt32(x, 16) + 2; //read and send the next bytes plus account for the trailing CRLF
                    if (length == 0) //get the header and add it to the 
                    {
                        buf = new byte[3];
                        BodyStream.Read(buf, 0, 3);
                        x = Encoding.ASCII.GetString(buf);
                        while (!x.Contains("\r\n"))
                        {
                            if (x.Contains("\r"))
                            {
                                BodyStream.Read(buf, 0, 1);
                                x += (char)buf[0];
                            }
                            else
                            {
                                BodyStream.Read(buf, 0, 2);
                                x += (char)buf[0];
                                x += (char)buf[1];
                            }
                            x = x.Replace("\r\n", "");
                        }
                    }
                    else
                    {
                        while (length > 0)
                        {
                            buf = new byte[length];
                            length -= BodyStream.Read(buf, 0, length);
                            ns.Write(buf, 0, buf.Length);
                        }
                    }
                }
            }
            else
            {
                byte[] header = Encoding.ASCII.GetBytes(Header);
                ns.Write(header, 0, header.Length);
                byte[] buf = readStream();
                try
                {
                    ns.Write(buf, 0, buf.Length);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
        }
        public byte[] readStream()
        {
            byte[] buf = new byte[1024];
            var ms = new MemoryStream();
            int bRead = BodyStream.Read(buf, 0, 1024);
            while (bRead != 0)
            {
                try
                {
                    ms.Write(buf, 0, bRead);
                    bRead = BodyStream.Read(buf, 0, 1024);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            return ms.GetBuffer();
        }
    }
}
