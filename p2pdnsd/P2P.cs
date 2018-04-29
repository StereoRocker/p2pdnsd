using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

/* P2P Packet Structure:
 * uint32 ID
 * uint8  isRequest
 * byte[] data
 * Where, if isRequest == 1, data is a string and, if isRequest == 0, data follows the structure below:
 * 
 * data:
 * uint8 exists
 * uint32 ttl
 * byte[4] ipv4
 */

namespace p2pdnsd
{
    public static class P2P
    {
        private static int _P2P_PORT = 15000;

        public static bool resolve(string name, ref byte[] address)
        {
            IPEndPoint broadcast_ep = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 15000);
            UdpClient client = new UdpClient();

            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.ExclusiveAddressUse = false;

            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            Random r = new Random();
            UInt32 id = (UInt32)r.Next();

            bw.Write(id);
            bw.Write((byte)1);  // isRequest
            DNS.WriteName(bw, name);

            byte[] packet = ms.ToArray();
            client.Send(packet, packet.Length, broadcast_ep);

            ArrayList packets = new ArrayList();
            client.Client.ReceiveTimeout = 10;

            try
            {
                while (true)
                {
                    IPEndPoint recv_ipep = new IPEndPoint(IPAddress.Any, _P2P_PORT);
                    byte[] packet_recv = client.Receive(ref recv_ipep);
#if DEBUG
                    Console.WriteLine("Received P2P from {0}", recv_ipep.ToString());
#endif

                    if (packet_recv.Length < 6)
                        continue;

                    MemoryStream ms_r = new MemoryStream(packet_recv);
                    BinaryReader br = new BinaryReader(ms_r);
                    UInt32 id_r = br.ReadUInt32();
                    if (id_r == id)
                    {
                        if (br.ReadByte() == 0)
                        {
                            packets.Add(packet_recv);
                        }
                    }
                }
            } catch (SocketException ex)
            {

            }

            // If no information was received, fail early
            if (packets.Count < 1)
            {
                return false;
            }

            // Collate received addresses
            Dictionary<byte[], int> addresses = new Dictionary<byte[], int>();
            foreach (byte[] buf in packets)
            {
                MemoryStream ms_b = new MemoryStream(buf);
                BinaryReader br = new BinaryReader(ms_b);

                // Skip ID, we already checked this
                br.ReadUInt32();
                // Skip isRequest, we already checked this
                br.ReadByte();

                byte exists = br.ReadByte();
                UInt32 ttl = br.ReadUInt32();
                byte[] addr = br.ReadBytes(4);

                if (exists == 0)
                {
                    addr = new byte[] { 0, 0, 0, 0 };
                }

                if (addresses.ContainsKey(addr))
                {
                    addresses[addr]++;
                } else
                {
                    addresses.Add(addr, 1);
                }
            }

            // Choose most likely address based on which was reported the most
            byte[] addr_picked = null;
            int max = 0;
            foreach (KeyValuePair<byte[], int> kvp in addresses)
            {
                if (kvp.Value > max)
                    addr_picked = kvp.Key;
            }

            if (addr_picked.Equals(new byte[] {0,0,0,0}))
            {
                return false;
            }
            address = addr_picked;
            return true;
        }

        public static void Run()
        {
            UdpClient udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            IPEndPoint recv_groupep = new IPEndPoint(IPAddress.Any, _P2P_PORT);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.Bind(recv_groupep);

            while (true)
            {
                IPEndPoint recv_ipep = null;
                try
                {
                    byte[] packet = udpClient.Receive(ref recv_ipep);
                    MemoryStream ms = new MemoryStream(packet);
                    BinaryReader br = new BinaryReader(ms);

                    UInt32 id = br.ReadUInt32();
                    byte isRequest = br.ReadByte();

                    if (isRequest == 1)
                    {
                        string name = DNS.ReadName(br);
                        DNScache.clean();
                        lock (DNScache._lock_cache)
                        {
                            if (DNScache._cache.ContainsKey(name))
                            {
                                MemoryStream ms_res = new MemoryStream();
                                BinaryWriter bw = new BinaryWriter(ms_res);
                                bw.Write(id);
                                bw.Write((byte)0);

                                DNScache.cache_entry ce = DNScache._cache[name];

                                if (ce.success)
                                {
                                    bw.Write((byte)1);
                                    UInt32 ttl = (UInt32)((ce.expiry - DateTime.Now).TotalSeconds);
                                    bw.Write((UInt32)IPAddress.HostToNetworkOrder((int)ttl));
                                    bw.Write(ce.address, 0, 4);
                                } else
                                {
                                    bw.Write((byte)0);
                                    UInt32 ttl = (UInt32)((ce.expiry - DateTime.Now).TotalSeconds);
                                    bw.Write((UInt32)IPAddress.HostToNetworkOrder((int)ttl));
                                    bw.Write((byte)0);
                                    bw.Write((byte)0);
                                    bw.Write((byte)0);
                                    bw.Write((byte)0);
                                }

                                byte[] packet_res = ms_res.ToArray();
                                udpClient.Send(packet_res, packet_res.Length, recv_ipep);
                            }
                        }
                    }

                } catch (ThreadInterruptedException ex) {
                    return;
                }
            }

            
        }
    }
}
