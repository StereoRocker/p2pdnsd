using System.Text;
using System.IO;

namespace p2pdnsd
{
    public static class DNS
    {
        public static string ReadName(BinaryReader br)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                int len = br.ReadByte();

                if ((len & 0xC0) > 0)
                {
                    // This string is compressed somewhere else


                    // Get the second byte of the pointer
                    int len_lo = br.ReadByte();
                    len = (len << 8) | len_lo;

                    // Get a reference to the base stream and record it's position
                    Stream s = br.BaseStream;
                    long pos = s.Position;

                    // Get the offset of the string and seek to it
                    int offset = (len & 0x3FFF);
                    s.Seek(offset, SeekOrigin.Begin);

                    // Create a new BinaryReader and call recursively with the new BinaryReader to get the correct string
                    BinaryReader new_br = new BinaryReader(s);
                    string n = ReadName(new_br);

                    // Seek back to the original position of the stream
                    s.Seek(pos, SeekOrigin.Begin);

                    // Return the string
                    return n;
                }

                if (len == 0)
                    break;

                if (sb.Length != 0)
                    sb.Append('.');

                byte[] str = br.ReadBytes(len);
                char[] chars = ASCIIEncoding.ASCII.GetChars(str);
                sb.Append(chars);
            }
            return sb.ToString();
        }

        public static void WriteName(BinaryWriter bw, string name)
        {
            string[] elements = name.Split('.');
            for (int i = 0; i < elements.Length; i++)
            {
                byte[] str = ASCIIEncoding.ASCII.GetBytes(elements[i]);
                bw.Write((byte)elements[i].Length);
                bw.Write(str);
            }
            bw.Write((byte)0);
        }
    }
}
