using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tunneler.Packet
{
    public class PackingHelpers
    {
        public static void PackUint64(UInt64 src, byte[] data, ref uint index)
        {
            data[index++] = (byte)(src & 0xff);
            data[index++] = (byte)((src >> 08) & 0xff);
            data[index++] = (byte)((src >> 16) & 0xff);
            data[index++] = (byte)((src >> 24) & 0xff);
            data[index++] = (byte)((src >> 32) & 0xff);
            data[index++] = (byte)((src >> 40) & 0xff);
            data[index++] = (byte)((src >> 48) & 0xff);
            data[index++] = (byte)((src >> 56) & 0xff);
        }

        public static void UnpackUint64(ref UInt64 dst, byte[] data, ref uint index)
        {
            dst = data[index++];
            dst += ((UInt64)data[index++]) << 08;
            dst += ((UInt64)data[index++]) << 16;
            dst += ((UInt64)data[index++]) << 24;
            dst += ((UInt64)data[index++]) << 32;
            dst += ((UInt64)data[index++]) << 40;
            dst += ((UInt64)data[index++]) << 48;
            dst += ((UInt64)data[index++]) << 56;
        }

        public static void UnpackUint32(ref UInt32 dst, byte[] data, ref uint index)
        {
            dst = data[index++];
            dst += ((UInt32)data[index++]) << 08;
            dst += ((UInt32)data[index++]) << 16;
            dst += ((UInt32)data[index++]) << 24;
        }

        public static void UnpackUint32(ref UInt32 dst, MemoryStream ms)
        {
            dst = (UInt32)ms.ReadByte();
            dst += ((UInt32)ms.ReadByte()) << 08;
            dst += ((UInt32)ms.ReadByte()) << 16;
            dst += ((UInt32)ms.ReadByte()) << 24;
        }

        public static void PackUint32(UInt32 src, byte[] data, ref uint index)
        {
            data[index++] = (byte)(src & 0xff);
            data[index++] = (byte)((src >> 08) & 0xff);
            data[index++] = (byte)((src >> 16) & 0xff);
            data[index++] = (byte)((src >> 24) & 0xff);
        }
    }
}
