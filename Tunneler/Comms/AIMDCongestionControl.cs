using System;
using System.Diagnostics;
using Tunneler.Comms;
using System.Security.Principal;

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
        //NOTE: TCPTAHOE will send out the last in order packet seq as it receives out of order ones.
        //NOTE: Also sendingrate = window size/RTT
        internal enum AIMDCongestionState
        {
            SlowStart,
            Linear
        }

		private UInt16 mMaxCongestionWindow;
        internal AIMDCongestionControl (IPacketSender packetSender, ushort interval, ushort datagramSize, 
                                        ushort congestionWindowSize = 1, int retransmitInterval = 50, ushort maxCongestionWindow = 20):
            base(packetSender, interval, datagramSize, congestionWindowSize, retransmitInterval)
        {
            this.mMaxCongestionWindow = maxCongestionWindow;
        }

        #region implemented abstract members of CongestionControlBase

        protected override void OnAcked (TimestampedPacket acked)
        {
            //todo: see if the ack is out of sequence
            if (this.CongestionWindowSize < this.mMaxCongestionWindow)
            {
				ushort newWindow = (ushort)Math.Min (this.CongestionWindowSize * 2, mMaxCongestionWindow);
				this.ChangeCongestionWindow((UInt16)newWindow);
            }
        }

        protected override void OnPacketsDropped (int totalPackets, int packetsDropped)
        {
            if (CongestionWindowSize > 0)
            {
                Debug.Assert((UInt16) (this.CongestionWindowSize/2) > 0);
                this.ChangeCongestionWindow((UInt16) (this.CongestionWindowSize/2));
            }
        }

        #endregion
    }
}

