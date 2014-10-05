using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Tunneler.Raw.IPv4
{
    internal class ICMPHeader
    {
        internal enum ICMPTypes
        {
            EchoReply = 0,
            DestinationUnreachable = 3,
            SourceQuench = 0,
            RedirectMessage = 5,
            EchoRequest = 8,
            RouterAdvertisement = 9,
            RouterSolicitation = 10,
            TimeExceeded = 11,
            BadIPHeader = 12,
            TimeStamp = 13,
            TimeStampReply = 14,
            InformationRequest = 15,
            InformationReply = 16,
            AddressMaskRequest = 17,
            AddressMaskReply = 18
        }

        private const int MAX_ICMP_SIZE = 500;
        private byte type;
        private byte code;
        private UInt16 checksum;
        private int messageSize;
        private byte[] rest;

        internal ICMPHeader(byte[] byBuffer, int nReceived)
        {
            if (nReceived <= MAX_ICMP_SIZE)
            {

                type = byBuffer[0];
                code = byBuffer[1];
                checksum = BitConverter.ToUInt16(byBuffer, 2);
                messageSize = byBuffer.Length - nReceived;
                rest = new byte[messageSize];
                Buffer.BlockCopy(byBuffer, 4, rest, 0, messageSize);
            }
        }

        internal ICMPTypes Type
        {
            get { return (ICMPTypes) type; }
        }

        internal int Code
        {
            get { return (int) code; }
        }

        internal static ushort ComputeHeaderIpChecksum(byte[] header, int start, int length)
        {

            ushort word16;
            long sum = 0;
            for (int i = start; i < (length + start); i += 2)
            {
                word16 = (ushort)(((header[i] << 8) & 0xFF00)
                + (header[i + 1] & 0xFF));
                sum += (long)word16;
            }

            while ((sum >> 16) != 0)
            {
                sum = (sum & 0xFFFF) + (sum >> 16);
            }

            sum = ~sum;

            return (ushort)sum;


        }
    }
}
