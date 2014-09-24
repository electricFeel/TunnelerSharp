using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tunneler.Packet;

namespace Tunneler.Comms
{
	class NoCongestionControl:CongestionControlBase
    {
        public NoCongestionControl(IPacketSender packetSender, ushort interval, ushort datagramSize, ushort congestionWindowSize = 1, int retransmitInterval = 500) : base(packetSender, interval, datagramSize, congestionWindowSize, retransmitInterval)
        {
        }

		protected override void OnAcked (TimestampedPacket acked)
		{

		}

        /// <summary>
        /// Overriding the base implementation to prevent the queues from growing
        /// </summary>
        /// <param name="packet"></param>
        internal override void SendPacket(GenericPacket packet)
        {
            this.packetSender.SendPacket(packet);
        }

		#region implemented abstract members of CongestionControlBase

		protected override void OnPacketDropped (int totalPackets, int packetsDropped)
		{

		}

		#endregion
    }
}
