using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tunneler.Comms
{
    /// <summary>
    /// Simple congestion control simply waits for an ack to be recieved
    /// for every packet sent and has a fixed size congestion window of 1
    /// </summary>
    class SimpleCongestionControl:CongestionControlBase
    {
        public SimpleCongestionControl(IPacketSender packetSender, ushort interval, ushort datagramSize, ushort congestionWindowSize = 1, int retransmitInterval = 500) 
            : base(packetSender, interval, datagramSize, congestionWindowSize, retransmitInterval)
        {
        }

        protected override void OnAcked(TimestampedPacket acked)
        {
            //do nothing the congestion window should never change size
        }

        protected override void OnPacketsDropped(int totalPackets, int packetsDropped)
        {
			//raise a disconnected event (?)
        }
    }
}
