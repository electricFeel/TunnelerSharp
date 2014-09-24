using System;
using System.Threading;
using System.Timers;
using C5;
using Tunneler.Packet;
using Timer = System.Timers.Timer;
using System.IO;

namespace Tunneler.Comms
{
    internal struct TimestampedPacket
    {
        internal GenericPacket packet;
        internal Int32 lastTransmissionTime;
        internal UInt16 retransmissionCount;
		internal bool timedOut;
	}

    /// <summary>
    /// Congestion control base class
    /// </summary>
    internal abstract class CongestionControlBase
    {
		/*
		 * Only the sending party cares about acks, therefore acks need to be a special
		 * type of message that is simply a passthrough to the congestion control, i.e. one that
		 * doesn't require a return from the client. 
		*/
        private object l_sendQueue = new object();
        private object l_ackQueue = new object();
        private ReaderWriterLockSlim rw_ackQueue = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim rw_sendQueue = new ReaderWriterLockSlim();
		private ReaderWriterLockSlim rw_congestionWindow = new ReaderWriterLockSlim ();

        private Timer timer;
        protected IPacketSender packetSender;

        protected IQueue<TimestampedPacket> sendQueue;
		protected IDictionary<UInt32, TimestampedPacket> ackQueue;
        /// <summary>
        /// Effective size of the congestion queue
        /// </summary>
        protected UInt16 EffectiveWindow 
        {
            get
            {
                rw_ackQueue.EnterReadLock();
				rw_congestionWindow.EnterReadLock ();
                var v = (UInt16) (CongestionWindowSize - ackQueue.Count);
				rw_congestionWindow.ExitReadLock ();
                rw_ackQueue.ExitReadLock();
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
        internal double  Interval { get; private set; }

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

            this.sendQueue = new LinkedList<TimestampedPacket>();
			this.ackQueue = new HashDictionary<uint, TimestampedPacket> ();
			this.RetransmitInterval = retransmitInterval;
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
			int timeoutCount = 0;
            rw_sendQueue.EnterWriteLock();
            //send anything left in the queue
            if (EffectiveWindow > 0 && sendQueue.Count > 0)
            {
                int toSend = sendQueue.Count - EffectiveWindow;
                while (EffectiveWindow > 0 && toSend > 0)
                {
                    TimestampedPacket timestampedPacket = sendQueue.Dequeue();
                    this.packetSender.SendPacket(timestampedPacket.packet);
                    timestampedPacket.lastTransmissionTime = Environment.TickCount;
                }
            }
            rw_sendQueue.ExitWriteLock();
            rw_ackQueue.EnterReadLock();
            //search over the ackqueue and look for anything that is over the transmission timeout to retransmit
            for (uint i = 0; i < ackQueue.Count; i++)
            {
				TimestampedPacket tsp = ackQueue[i];
                if (tsp.lastTransmissionTime - Environment.TickCount > RetransmitInterval)
                {
                    this.packetSender.SendPacket(tsp.packet);
                    tsp.lastTransmissionTime = Environment.TickCount;
                    tsp.retransmissionCount++;
					if (tsp.retransmissionCount > this.RetransmitInterval) {
						tsp.timedOut = true;
						timeoutCount++;
					}
                }
            }
            rw_ackQueue.ExitReadLock();
			if(timeoutCount > 0){
				OnPacketDropped (ackQueue.Count, timeoutCount);
			}
        }

		internal void ChangeCongestionWindow(UInt16 newWindow){
			rw_congestionWindow.EnterWriteLock ();
			this.CongestionWindowSize = newWindow;
			rw_congestionWindow.ExitWriteLock ();
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

            if (EffectiveWindow > 0)
            {
                //todo: have the packet sender itself manage applying the timestamp (in this case the tunnelsocket which may need to be made threadsafe)
                this.packetSender.SendPacket(tsp.packet);
                tsp.lastTransmissionTime = Environment.TickCount;

                rw_ackQueue.EnterWriteLock();
				ackQueue.Add(tsp.packet.Seq, tsp);
                rw_ackQueue.ExitWriteLock();
            }
            else
            {
                rw_sendQueue.EnterWriteLock();
                sendQueue.Enqueue(tsp);
                rw_sendQueue.ExitWriteLock();
            }
        }

		protected abstract void OnAcked (TimestampedPacket acked);
		protected abstract void OnPacketDropped(int totalPackets, int packetsDropped);

		internal void Acked(UInt32 id){
			rw_ackQueue.EnterWriteLock ();
			TimestampedPacket output;
			ackQueue.Remove (id, out output);
			rw_ackQueue.ExitWriteLock ();
			OnAcked (output);
		}

		internal void SendAck(GenericPacket packet){
			this.packetSender.SendPacket (packet);
		}
    }
}
