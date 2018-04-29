using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace p2pdnsd
{
    class Program
    {
        private const int listenPort = 53;

        static void Main(string[] args)
        {
            // Start P2P thread
            Thread p2pListener = new Thread(new ThreadStart(P2P.Run));
            p2pListener.Start();

            // Open UDP socket
            UdpClient listener = new UdpClient(listenPort);
            IPEndPoint groupEP;

            // Receive a packet
            try
            {
                byte[] recv_buf;
                while (true)
                {
                    // Set listen address
                    groupEP = new IPEndPoint(IPAddress.Any, listenPort);

                    // Receive data, client will be placed in groupEP
                    recv_buf = listener.Receive(ref groupEP);

                    // Parse DNS data
                    MemoryStream ms = new MemoryStream(recv_buf);
                    BinaryReader br = new BinaryReader(ms);

                    DNS_header header = new DNS_header();
                    header.Read(br);

                    if (header.QDcount != 1)
                    {
                        Console.WriteLine("Cannot parse DNS request where QDcount != 1");
                        continue;
                    }

                    DNS_question question = new DNS_question();
                    question.Read(br);

                    // Get the answer to the query
                    byte[] ans_ip = new byte[4];
                    DNScache.resolve(question.name, ref ans_ip);

                    // Send a response
                    MemoryStream ms_out = new MemoryStream();
                    BinaryWriter bw = new BinaryWriter(ms_out);

                    // Modify DNS header
                    header.qr = true;
                    header.rcode = 0;
                    header.ANcount = 1;

                    // Write DNS header
                    header.Write(bw);

                    // Write question
                    question.Write(bw);

                    // Generate answer
                    DNS_answer answer = new DNS_answer();
                    answer.name = question.name;
                    answer.atype = 1;
                    answer.aclass = 1;
                    answer.ttl = 60;
                    answer.rdlen = 4;
                    answer.rdata = ans_ip;

                    // Write answer
                    answer.Write(bw);

                    // Send the packet
                    byte[] buf_out = ms_out.ToArray();
                    listener.Send(buf_out, buf_out.Length, groupEP);
                }
            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }

            p2pListener.Interrupt();
        }
    }
}
