using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.IO;

namespace p2pdnsd
{
    static class DNScache
    {
        public struct cache_entry {
            public bool success;
            public byte[] address;
            public DateTime expiry;
        }

        public static Dictionary<string, cache_entry> _cache;
        public static object _lock_cache = new object();

        private static Logger log = new Logger("dns");

        static DNScache()
        {
            _cache = new Dictionary<string, cache_entry>();

            Logger.Start(new FileInfo("dns.log"));
        }

        public static void clean()
        {
            lock (_lock_cache)
            {
                List<string> toRemove = new List<string>();
                foreach (KeyValuePair<string, cache_entry> kvp in _cache)
                {
                    if (kvp.Value.expiry < DateTime.Now)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (string s in toRemove)
                {
                    _cache.Remove(s);
                }
            }
        }

        public static bool resolve(string name, ref byte[] result)
        {
            // If the cache contains the result, return it
            lock (_lock_cache)
            {
                if (_cache.ContainsKey(name))
                {
                    cache_entry ce = _cache[name];
                    if (ce.expiry < DateTime.Now)
                    {
                        _cache.Remove(name);
                    }
                    else
                    {
                        if (ce.success)
                        {
                            Array.Copy(ce.address, result, 4);
                        }
#if DEBUG
                    Console.WriteLine("Resolved through local cache!");
#endif
                        log.Info("cache");
                        return ce.success;
                    }
                }
            }

            // Query P2P before delegating to real DNS server
            if (P2P.resolve(name, ref result))
            {
#if DEBUG
                Console.WriteLine("Resolved through P2P!");
#endif
                log.Info("p2p");
                return true;
            }

            // Otherwise, we must query a real DNS server
            // TODO: Change from hardcoded DNS server to configurable server
            DNS_header req = new DNS_header();
            req.ID = 1337;
            req.qr = false;
            req.opcode = 0;
            req.tc = false;
            req.rd = true;
            req.QDcount = 1;
            req.ANcount = 0;
            req.NScount = 0;
            req.ARcount = 0;

            DNS_question req_q = new DNS_question();
            req_q.name = name;
            req_q.qclass = 1;
            req_q.qtype = 1;

            // Set up the packet
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            req.Write(bw);
            req_q.Write(bw);

            // Send the packet
            byte[] req_buf = ms.ToArray();
            UdpClient client = new UdpClient();
            client.Connect(IPAddress.Parse("8.8.8.8"), 53);
            client.Send(req_buf, req_buf.Length);

            // Receive a packet
            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
            client.Client.ReceiveTimeout = 1000;
            byte[] res_buf;
            try
            {
                res_buf = client.Receive(ref ipep);
            } catch (SocketException ex)
            {
                return false;
            }
            MemoryStream res_ms = new MemoryStream(res_buf);
            BinaryReader res_br = new BinaryReader(res_ms);
            DNS_header res_h = new DNS_header();
            res_h.Read(res_br);

            if (!res_h.qr)
            {
                Console.WriteLine("Received packet was a query!");
                return false;
            }

            if (res_h.rcode != 0)
            {
                lock (_lock_cache)
                {
                    cache_entry ce = new cache_entry();
                    ce.success = false;
                    ce.expiry = DateTime.Now.AddMinutes(15);
                    _cache.Add(name, ce);
                }
                return false;
            }

            DNS_question[] res_qs = new DNS_question[res_h.QDcount];
            DNS_answer[] res_as = new DNS_answer[res_h.ANcount];

#if DEBUG
            Console.WriteLine("Questions: {0}\nAnswers: {1}", res_h.QDcount, res_h.ANcount);
#endif

            for (int i = 0; i < res_h.QDcount; i++)
            {
                res_qs[i] = new DNS_question();
                res_qs[i].Read(res_br);

#if DEBUG
                Console.WriteLine("Question {0}: {1}\n", i, res_qs[i].name);
#endif
            }

            byte[] address = new byte[4];
            uint ttl = 0;
            bool answer = false;
            for (int i = 0; i < res_h.ANcount; i++)
            {
                res_as[i] = new DNS_answer();
                res_as[i].Read(res_br);

#if DEBUG
                Console.WriteLine("Answer {0}: {1}\n", i, res_as[i].name);
#endif
                if (res_as[i].atype == 1)
                {
#if DEBUG
                    Console.WriteLine("{0}.{1}.{2}.{3} TTL: {4}", res_as[i].rdata[0], res_as[i].rdata[1], res_as[i].rdata[2], res_as[i].rdata[3], res_as[i].ttl);
#endif
                    for (int j = 0; j < 4; j++)
                    {
                        address[j] = res_as[i].rdata[j];
                        ttl = res_as[i].ttl;
                        answer = true;
                    }
                }
            }

            if (answer)
            {
                lock (_lock_cache)
                {
                    cache_entry ce = new cache_entry();
                    ce.success = true;
                    ce.address = address;
                    ce.expiry = DateTime.Now.AddSeconds(ttl);
                    _cache.Add(name, ce);
                }

                for (int i = 0; i < 4; i++)
                {
                    result[i] = address[i];
                }

#if DEBUG
                Console.WriteLine("Resolved through DNS!");
#endif
                log.Info("dns");
                return true;
            }

            return false;
        }
    }
}
