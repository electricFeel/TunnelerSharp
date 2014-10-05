using System;
using System.Diagnostics;
using Tunneler;
using Tunneler.Comms;
using TunnelerTestWin.mocks;
using Tunneler.Packet;

namespace TunnelerTestWin
{
    /// <summary>
    /// A mock object specifically intended to test the congestion control mechanisims. It acts 
	/// as a tunnel.
    /// </summary>
    public class TunnelCongestionControllerMock
    {
        private IPacketSender packetSender;
        private UInt32 curSeq = 1;
        CongestionControlBase congestionController;
        private Action<GenericPacket> incomingPacketInterceptor;

        internal TunnelCongestionControllerMock (CongestionControlBase congestionControl,
                                                 IPacketSender socketMock)
        {
            congestionController = congestionControl;
            packetSender = socketMock;
        }

        public void SendPacket (Tunneler.Packet.GenericPacket p)
        {
            congestionController.SendPacket (p);
        }

        public void HandleIncomingPacket (GenericPacket p)
        {
            Console.WriteLine(String.Format("Incoming packet Seq#{0} Ack#{1}", p.Seq, p.Ack));
            if (congestionController.CheckPacket(p))
            {
                Console.WriteLine(String.Format("Packet ACCEPTED because its in of order Seq#{0} Ack#{1}", p.Seq, p.Ack));
                if (this.incomingPacketInterceptor != null) incomingPacketInterceptor.Invoke(p);
                GenericPacket ackPacket = new GenericPacketMock(0);
                ackPacket.Ack = p.Seq;
                congestionController.SendAck(ackPacket);
            }
			else if(p.Seq != 0)
            {
                Console.WriteLine(String.Format("Packet IGNORED because its out of order Seq#{0} Ack#{1}", p.Seq, p.Ack));
            }
        }

        public void SetIncomingPacketInterceptor(Action<GenericPacket> action)
        {
            this.incomingPacketInterceptor = action;
        }
    }
}

