using System;
using Tunneler.Comms;

namespace Tunneler
{
	/// <summary>
	/// AIMD (Additive Increase Multiplicitive Decrease) simply increases the size of the 
	/// congestion window by adding one to the size of the window each time an ack
	/// is received and decreases the size of the window by half when the 
	/// a packet drop is detected. 
	/// 
	/// Packet loss is the only signal for packet loss in this scheme. 
	/// </summary>
	internal class AIMDCongestionControl : CongestionControlBase
	{
		internal AIMDCongestionControl (IPacketSender packetSender, ushort interval, ushort datagramSize, 
										ushort congestionWindowSize = 1, int retransmitInterval = 500):
			base(packetSender, interval, datagramSize, congestionWindowSize, retransmitInterval)
		{

		}

		#region implemented abstract members of CongestionControlBase

		protected override void OnAcked (TimestampedPacket acked)
		{
			this.ChangeCongestionWindow ((UInt16)(this.CongestionWindowSize + 1));
		}

		protected override void OnPacketDropped (int totalPackets, int packetsDropped)
		{
			this.ChangeCongestionWindow ((UInt16)(this.CongestionWindowSize / 2));
		}

		#endregion
	}
}

