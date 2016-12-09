using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProxyServer
{
    public class WebBase
    {
        public string Header { get; set; } //unmodified header
        public Dictionary<string, string> Headers { get; set; }
        public Stream BodyStream { get; set; }
        public bool KeepAlive { get; set; }

        public List<string> ParseRequest(NetworkStream s, out byte[] b)
        {
            List<string> lines = new List<string>();
            StringBuilder header = new StringBuilder();

            byte[] buffer = new byte[2048];
            StringBuilder builder = new StringBuilder();

            bool EOH = false; //strop reading once end of header
            byte[] bout = null;

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

                            bout = new byte[bRead - i];
                            for (int j = 0; i < bRead; j++, i++) //read the start of the body in
                            {
                                bout[j] = buffer[i];
                            }
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
            b = bout;
            return lines;
        }


        public void Forward(NetworkStream ns)
        {
            if (Headers.ContainsKey("transfer-encoding"))
            {
                byte[] header = Encoding.ASCII.GetBytes(Header);
                ns.Write(header, 0, header.Length);

                while (true) //while we are still using the current message
                {
                    //now read the next byte until CRLF and remove CRLF this value is x where x is a hex representation of lenth.
                    string x = "";
                    int i = 0;
                    byte[] buf = new byte[128];
                    while (!x.Contains("\r\n"))
                    {
                        if (x.Contains("\r"))
                        {
                            i += BodyStream.Read(buf, i, 1);
                            x += (char)buf[i - 1];
                        }
                        else
                        {
                            i += BodyStream.Read(buf, i, 2);
                            x += (char)buf[i - 2];
                            x += (char)buf[i - 1];
                        }
                    }
                    x = x.Replace("\r\n", "");
                    int length = Convert.ToInt32(x, 16); //read and send the next bytes
                    ns.Write(buf, 0, i);
                    if (length == 0) //get the headers and add it to the 
                    {
                        while (true)
                        {
                            i = 0;
                            x = "";
                            buf = new byte[128];
                            while (!x.Contains("\r\n"))
                            {
                                if (x.Contains("\r"))
                                {
                                    i += BodyStream.Read(buf, i, 1);
                                    x += (char)buf[i - 1];
                                }
                                else
                                {
                                    i += BodyStream.Read(buf, i, 2);
                                    x += (char)buf[i - 2];
                                    x += (char)buf[i - 1];
                                }
                            }
                            x = x.Replace("\r\n", "");
                            ns.Write(buf, 0, i);
                            if (x != "")
                            {
                                string[] helper = x.Split(':');
                                Headers[helper[0]] = helper[1];

                            }
                            else { return; }
                        }
                    }
                    else
                    {
                        length += 2; //account for trailing CRLF
                        int read = 0;
                        while (length > 0)
                        {
                            buf = new byte[4096];
                            length -= read = BodyStream.Read(buf, 0, length > 4096 ? 4096 : length);
                            ns.Write(buf, 0, read);
                        }
                    }
                }
            }
            else
            {
                byte[] header = Encoding.ASCII.GetBytes(Header);
                ns.Write(header, 0, header.Length);
                byte[] buf = new byte[1024];
                int bRead = BodyStream.Read(buf, 0, 1024);
                while (bRead != 0)
                {
                    try
                    {
                        ns.Write(buf, 0, bRead);
                        bRead = BodyStream.Read(buf, 0, 1024);
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                }
            }
        }
    }
}
