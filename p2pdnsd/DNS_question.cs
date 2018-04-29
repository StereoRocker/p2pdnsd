using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.IO;

namespace p2pdnsd
{
    class DNS_question
    {
        /* Public variables */
        public string name;
        public ushort qtype;
        public ushort qclass;

        /* Endian conversions */
        private void ntohs(ref ushort val)
        {
            val = (ushort)IPAddress.NetworkToHostOrder((short)val);
        }

        private void htons(ref ushort val)
        {
            val = (ushort)IPAddress.HostToNetworkOrder((short)val);
        }

        private void ntoh()
        {
            ntohs(ref qtype);
            ntohs(ref qclass);
        }

        private void hton()
        {
            htons(ref qtype);
            htons(ref qclass);
        }

        /* Instance management */
        public void Read(BinaryReader br)
        {
            name = DNS.ReadName(br);
            qtype = br.ReadUInt16();
            qclass = br.ReadUInt16();
            ntoh();
        }

        public void Write(BinaryWriter bw)
        {
            hton();
            DNS.WriteName(bw, name);
            bw.Write(qtype);
            bw.Write(qclass);
            ntoh();
        }
    }
}
