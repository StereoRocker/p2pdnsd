using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using System.Net;

namespace p2pdnsd
{
    class DNS_header
    {
        /* Public variables */
        public ushort ID;
        public ushort flags;
        public ushort QDcount;
        public ushort ANcount;
        public ushort NScount;
        public ushort ARcount;

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
            ntohs(ref ID);
            ntohs(ref flags);
            ntohs(ref QDcount);
            ntohs(ref ANcount);
            ntohs(ref NScount);
            ntohs(ref ARcount);
        }

        private void hton()
        {
            htons(ref ID);
            htons(ref flags);
            htons(ref QDcount);
            htons(ref ANcount);
            htons(ref NScount);
            htons(ref ARcount);
        }

        /* Instance management */
        public void Read(BinaryReader br)
        {
            ID = br.ReadUInt16();
            flags = br.ReadUInt16();
            QDcount = br.ReadUInt16();
            ANcount = br.ReadUInt16();
            NScount = br.ReadUInt16();
            ARcount = br.ReadUInt16();
            ntoh();
        }

        public void Write(BinaryWriter bw)
        {
            hton();
            bw.Write(ID);
            bw.Write(flags);
            bw.Write(QDcount);
            bw.Write(ANcount);
            bw.Write(NScount);
            bw.Write(ARcount);
            ntoh();
        }

        /* Utility getters and setters */

        public bool qr
        {
            get
            {
                return (flags & 0x8000) > 0;
            }

            set
            {
                byte flag = (byte)(value ? 1 : 0);
                flags = (ushort)(flags & 0x7FFF);
                flags = (ushort)(flags | (flag << 15));
            }
        }

        public byte opcode
        {
            get
            {
                return (byte) ((flags >> 11) & 0x0F);
            }

            set
            {
                byte oc = (byte)(value & 0x0F);
                flags = (ushort)(flags & 0x87FF);
                flags = (ushort)(flags | (oc << 11));
            }
        }

        public bool aa
        {
            get
            {
                return (flags & 0x400) > 0;
            }

            set
            {
                byte flag = (byte)(value ? 1 : 0);
                flags = (ushort)(flags & 0xFBFF);
                flags = (ushort)(flags | (flag << 10));
            }
        }

        public bool tc
        {
            get
            {
                return (flags & 0x200) > 0;
            }

            set
            {
                byte flag = (byte)(value ? 1 : 0);
                flags = (ushort)(flags & 0xFDFF);
                flags = (ushort)(flags | (flag << 9));
            }
        }

        public bool rd
        {
            get
            {
                return (flags & 0x100) > 0;
            }

            set
            {
                byte flag = (byte)(value ? 1 : 0);
                flags = (ushort)(flags & 0xFEFF);
                flags = (ushort)(flags | (flag << 8));
            }
        }

        public bool ra
        {
            get
            {
                return (flags & 0x80) > 0;
            }
            
            set
            {
                byte flag = (byte)(value ? 1 : 0);
                flags = (ushort)(flags & 0xFF7F);
                flags = (ushort)(flags | (flag << 7));
            }
        }

        public byte rcode
        {
            get
            {
                return (byte)(flags & 0x0F);
            }

            set
            {
                byte rc = (byte)(value & 0x0F);
                flags = (ushort)(flags & 0xFFF0);
                flags = (ushort)(flags | rc);
            }
        }
    }
}
