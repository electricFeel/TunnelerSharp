using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tunneler.Packet;
using Tunneler.Raw;
using Tunneler.Raw.IPv4;

namespace Tunneler
{
    /// <summary>
    /// A version of the TunnelSocket that binds to a raw socket for the purpose of 
    /// 
    /// </summary>
    class TunnelSocketRaw:TunnelSocket
    {
        private Socket outputSocket;
        public TunnelSocketRaw(short port) : base(port)
        {
            
        }

        public override void Start()
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            this.socket.DontFragment = true;
            this.socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            socket.Bind(ep);

            this.outputSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.outputSocket.DontFragment = true;
            this.outputSocket.Bind(ep);

            isOn = true;
            ThreadStart start = new ThreadStart(StartRecievingPackets);
            receiverThread = new Thread(start);
            receiverThread.Start();
        }

        private void StartRecievingPackets()
        {
            byte[] byTrue = new byte[4]{1, 0, 0, 0};
            byte[] byOut = new byte[4];
            this.socket.IOControl(IOControlCode.ReceiveAll, byTrue, byOut);

            while (this.isOn)
            {
                Console.WriteLine("Entering receive loop");
                readerEvent.Reset();
                RawDGram p = new RawDGram(this.mtu, ep);
                socket.BeginReceiveFrom(p.rawBytes,
                                              0,
                                              p.rawBytes.Length,
                                              SocketFlags.None,
                                              ref p.sender,
                                              new AsyncCallback(OnPacketRecieved),
                                              p);
                readerEvent.WaitOne();
            }
        }

        public override void SendPacket(GenericPacket p)
        {
            Console.WriteLine("Sending packet");
            //base.SendPacket(p);
            byte[] buffer = p.ToBytes();
            this.outputSocket.BeginSendTo(p.ToBytes(), 0, buffer.Length, SocketFlags.None, p.destination,
                                          new AsyncCallback(OnPacketSent),
                                          p);
        }

        private void OnPacketRecieved(IAsyncResult ar)
        {
            Console.WriteLine("Data received");
            RawDGram p = (RawDGram) ar.AsyncState;
            int len = socket.EndReceiveFrom(ar, ref p.sender);
            readerEvent.Set();
            //this can be pushed off to a task or threadpool
            ParseHeader(p, len);
        }

        private void ParseHeader(RawDGram p, int len)
        {
            Console.WriteLine("Parsing Header");
            IPHeader header = new IPHeader(p.rawBytes, len);
            switch (header.ProtocolType)
            {
                case Protocol.ICMP:
                    ICMPHeader icmpHeader = new ICMPHeader(header.Data, header.MessageLength);
                    //Console.WriteLine(String.Format("ICMP type: {0} code: {1}", icmpHeader.Type, icmpHeader.Code));
                    if (icmpHeader.Type == ICMPHeader.ICMPTypes.DestinationUnreachable && icmpHeader.Code == 3)
                    {
                        //Binary search the MTU (this is used during the probe phase of some congestion control
                        //algorithms. Also, the initial congestion window should be calculated in terms of bytes i.e. (mtu/windowsize = packets)

                    }
                    break;
                case Protocol.TCP:
                    //Console.WriteLine("TCP packet received");
                    break;
                case Protocol.UDP:
                    UDPHeader udpHeader = new UDPHeader(header.Data, header.MessageLength);
                    HandleUDP(header, udpHeader);
                    break;
                case Protocol.Unknown:
                    //Console.WriteLine("Unknown packet received");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Handles the specific UDP sockets
        /// </summary>
        /// <param name="ipHeader">The IP Header</param>
        /// <param name="udpHeader">The UDP Header</param>
        private void HandleUDP(IPHeader ipHeader, UDPHeader udpHeader)
        {
            //todo: add a checksum verification
            //Console.WriteLine("UDP Packet Received");
            EncryptedPacket p = new EncryptedPacket(mtu);
            p.rawBytes = udpHeader.Data;
            p.sender = new IPEndPoint(ipHeader.SourceAddress, udpHeader.DestinationPort);
            p.UnpackHeader();
            base.HandlePacket(p);
        }

        /// <summary>
        /// TODO
        /// </summary>
        private void HandleICMP()
        {
            
        }
    }

    internal class RawDGram
    {
        internal byte[] rawBytes;
        internal EndPoint sender;
        internal RawDGram(int mtuSize, EndPoint sender)
        {
            rawBytes = new byte[mtuSize];
            this.sender = sender;
        }
    }
}
