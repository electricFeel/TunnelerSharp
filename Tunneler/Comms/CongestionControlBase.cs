using System;
using System.Threading;
using System.Timers;
using C5;
using Tunneler.Packet;
using Timer = System.Timers.Timer;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections;

namespace Tunneler.Comms
{
    public struct TimestampedPacket
    {
        internal GenericPacket packet;
        internal Int32 lastTransmissionTime;
        internal Int32 initialTransmissionTime;
        internal UInt16 retransmissionCount;
        internal bool timedOut;
    }

    /// <summary>
    /// Congestion control base class. A congestion control class is responsible for maintaing the 
    /// congestion window (in bytes) and the mtu size. It can use a number of signals to determine cwnd and
    /// mtu size including latency, packet loss, ICMP messages.
    /// 
    /// We're going to have to eventually move all the logic for handling messages and encryption here
    /// but lets keep the design as is for now (i.e. we're going to need a shared object encryptor with
    /// the SecureTunnel).
    /// </summary>
    public abstract class CongestionControlBase
    {
        const float RTT_CONST = 0.5f;
        /*
         * Only the sending party cares about acks, therefore acks need to be a special
         * type of message that is simply a passthrough to the congestion control, i.e. one that
         * doesn't require a return from the client. 
        */
        private ReaderWriterLockSlim rw_rttCalc = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim rw_congestionWindow = new ReaderWriterLockSlim();

        private Timer timer;
        protected UInt32 lastSeqNum = 0;
        protected IPacketSender packetSender;

		internal ConcurrentQueue<TimestampedPacket> sendQueue;
		internal ConcurrentDictionary<UInt32, TimestampedPacket> acks;
        protected Int32 RoundTripTime { get; set; }
        /// <summary>
        /// Effective size of the congestion queue
        /// </summary>
        protected UInt16 EffectiveWindow
        {
            get
            {
                rw_congestionWindow.EnterReadLock();
				var v = (UInt16)(CongestionWindowSize - acks.Count);
                rw_congestionWindow.ExitReadLock();
                return v;
            }
        }
        /// <summary>
        /// Maintains the number of packets that are currently in flight waiting for
        /// acks. After a specific timeout, we should consider the connection 
        /// as disconnected.
        /// </summary>
        internal UInt16 CongestionWindowSize { get; private set; }

        /// <summary>
        /// Represents a maximum congestion window size (i.e. no more than the number of packets
        /// in that congestion window can be sent
        /// </summary>
        protected UInt16 MaximumCongestionWindow { get; set; }

        /// <summary>
        /// Interval at which acks, congestion, timeouts etc are calculated in milliseconds
        /// </summary>
        internal double Interval { get; private set; }

        /// <summary>
        /// Max transmission size (maybe make this adjustable)?
        /// </summary>
        internal UInt16 DatagramSize { get; private set; }

        /// <summary>
        /// Number of times to retransmit before its considered a timeout
        /// </summary>
        /// <value>The retransmit interval.</value>
        protected int RetransmitInterval { get; set; }

        /// <summary>
        /// Basic constructor for all Congestion Control mechanisims. 
        /// </summary>
        /// <param name="congestionWindowSize"></param>
        /// <param name="interval"></param>
        /// <param name="datagramSize"></param>
        internal CongestionControlBase(IPacketSender packetSender, UInt16 interval, UInt16 datagramSize, UInt16 congestionWindowSize = 1, int retransmitInterval = 500)
        {
            this.CongestionWindowSize = congestionWindowSize;
            this.Interval = interval;
            this.DatagramSize = datagramSize;
            this.timer = new Timer(this.Interval);
            this.timer.Elapsed += TimerOnElapsed;
            this.packetSender = packetSender;
			//todo: convert the ackqueue to use this concurrent dictionary and eliminate the 
			//locks
			acks = new ConcurrentDictionary<uint, TimestampedPacket>();
            this.sendQueue = new ConcurrentQueue<TimestampedPacket>();
            this.RetransmitInterval = retransmitInterval;
            this.RoundTripTime = 1;
            this.timer.Start();
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {

            //send anything left in the queue
            if (EffectiveWindow > 0 && sendQueue.Count > 0)
            {
                int toSend = sendQueue.Count - EffectiveWindow;
                while (EffectiveWindow > 0 && toSend > 0)
                {

					TimestampedPacket timestampedPacket;
					if (this.sendQueue.TryDequeue (out timestampedPacket)) {
						this.SendTimestampedPacket (timestampedPacket);
					}
                }
            }
            
            //search over the ackqueue and look for anything that is over the transmission timeout to retransmit
			var enumerator = acks.GetEnumerator ();
			while(enumerator.MoveNext ()){
				TimestampedPacket tsp = enumerator.Current.Value;
				int tick = Environment.TickCount - tsp.lastTransmissionTime;

				if ( tick >= RetransmitInterval)
				{
					Console.WriteLine(String.Format("Resending packet Seq#:{0}", tsp.packet.Seq));
					this.packetSender.SendPacket(tsp.packet);
					tsp.lastTransmissionTime = Environment.TickCount;
					tsp.retransmissionCount++;
					if (tsp.retransmissionCount > this.RetransmitInterval)
					{
						tsp.timedOut = true;
						this.PacketDropped(tsp.retransmissionCount, tsp.lastTransmissionTime);
					}
				}
			}
            /*for (uint i = 0; i < acks.Count; i++)
            {
				Console.WriteLine ("Resending");
				TimestampedPacket tsp = acks.GetEnumerator ().
				int tick = Environment.TickCount - tsp.lastTransmissionTime;

				if ( tick >= RetransmitInterval)
                {
                    Console.WriteLine(String.Format("Resending packet Seq#:{0}", tsp.packet.Seq));
                    this.packetSender.SendPacket(tsp.packet);
                    tsp.lastTransmissionTime = Environment.TickCount;
                    tsp.retransmissionCount++;
                    if (tsp.retransmissionCount > this.RetransmitInterval)
                    {
                        tsp.timedOut = true;
                        this.PacketDropped(tsp.retransmissionCount, tsp.lastTransmissionTime);
                    }
                }
            }*/
         
        }

        internal void PacketDropped(int retryCount, int timeoutMilliseconds)
        {
            this.OnPacketsDropped(retryCount, timeoutMilliseconds);
        }

        internal void ChangeCongestionWindow(UInt16 newWindow)
        {
            rw_congestionWindow.EnterWriteLock();
            this.CongestionWindowSize = newWindow;
            rw_congestionWindow.ExitWriteLock();
        }

        /// <summary>
        /// Enqueues a packet to be sent out the wire
        /// </summary>
        /// <param name="packet"></param>
        internal virtual void SendPacket(GenericPacket packet)
        {
            TimestampedPacket tsp = new TimestampedPacket();
            tsp.packet = packet;
            tsp.lastTransmissionTime = Int32.MinValue;
            tsp.retransmissionCount = 0;
            tsp.timedOut = false;

			SendTimestampedPacket (tsp);
        }

		void SendTimestampedPacket (TimestampedPacket tsp)
		{
			if (EffectiveWindow > 0) {
				//todo: have the packet sender itself manage applying the timestamp (in this case the tunnelsocket which may need to be made threadsafe)
				tsp.lastTransmissionTime = Environment.TickCount;
				if (tsp.initialTransmissionTime == Int32.MinValue)
					tsp.initialTransmissionTime = tsp.lastTransmissionTime;
				acks.TryAdd (tsp.packet.Seq, tsp);
				this.packetSender.SendPacket (tsp.packet);
			}
			else {
				sendQueue.Enqueue (tsp);
			}
		}

        /// <summary>
        /// Ensure that the packet can be handled in order. 
        /// </summary>
        /// <returns><c>true</c>, if packet was handled, <c>false</c> otherwise.</returns>
        /// <param name="packet">Packet.</param>
        internal bool CheckPacket(GenericPacket packet)
        {
            if (packet.Ack != 0)
            {
                this.Acked(packet.Ack);
            }

            if (lastSeqNum + 1 == packet.Seq)
            {
                lastSeqNum = packet.Seq;
                return true;
            }
            return false;
        }

        protected abstract void OnAcked(TimestampedPacket acked);
        protected abstract void OnPacketsDropped(int totalPackets, int packetsDropped);

        internal void Acked(UInt32 id)
        {
            TimestampedPacket output;
			bool removed = acks.TryRemove (id, out output);
			Debug.Assert (removed, "Couldn't remove the seq id");
			Debug.Assert (this.EffectiveWindow > 0);
            OnAcked(output);
        }

        internal void RecalculateRTT(TimestampedPacket ackedPacket, Int32 timestamp)
        {
            rw_rttCalc.EnterWriteLock();
            //optomistic calculation that assumes the last transmit is more indiciative of 
            //future transmits in terms of RTT
            this.RoundTripTime = (int)(RTT_CONST * this.RoundTripTime) + (int)((1 - RTT_CONST) * (ackedPacket.lastTransmissionTime - timestamp));
            rw_rttCalc.ExitWriteLock();
        }

        internal void SendAck(GenericPacket packet)
        {
            this.packetSender.SendPacket(packet);
        }
    }
}
