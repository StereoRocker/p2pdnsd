using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.IO;

namespace p2pdnsd
{
    class DNS_answer
    {
        /* Public variables */
        public string name;
        public ushort atype;
        public ushort aclass;
        public UInt32 ttl;
        public ushort rdlen;
        public byte[] rdata;

        /* Endian conversions */
        private void ntohs(ref ushort val)
        {
            val = (ushort)IPAddress.NetworkToHostOrder((short)val);
        }

        private void htons(ref ushort val)
        {
            val = (ushort)IPAddress.HostToNetworkOrder((short)val);
        }

        public void ntoh()
        {
            ntohs(ref atype);
            ntohs(ref aclass);
            ttl = (UInt32)IPAddress.NetworkToHostOrder((int)ttl);
            ntohs(ref rdlen);
        }

        public void hton()
        {
            htons(ref atype);
            htons(ref aclass);
            ttl = (UInt32)IPAddress.HostToNetworkOrder((int)ttl);
            htons(ref rdlen);
        }

        /* Instance management */
        public void Read(BinaryReader br)
        {
            name = DNS.ReadName(br);
            atype = br.ReadUInt16();
            aclass = br.ReadUInt16();
            ttl = br.ReadUInt32();
            rdlen = br.ReadUInt16();
            ntoh();
            rdata = br.ReadBytes((int)rdlen);
        }

        public void Write(BinaryWriter bw)
        {
            hton();
            DNS.WriteName(bw, name);
            bw.Write(atype);
            bw.Write(aclass);
            bw.Write(ttl);
            bw.Write(rdlen);
            ntoh();
            bw.Write(rdata, 0, (int)rdlen);
        }
    }
}
