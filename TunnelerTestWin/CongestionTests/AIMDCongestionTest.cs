using System;
using NUnit.Framework;
using Tunneler;
using TunnelerTestWin.mocks;
using Tunneler.Comms;

namespace TunnelerTestWin
{
    [TestFixture()]
    public class AIMDCongestionTest:CongestionReliablityTestBase
    {
        private TunnelSocketMock socketMock;
        private AIMDCongestionControl congestion;

        [SetUp]
        public void Setup()
        {
            socketMock = new TunnelSocketMock();
            congestion = new AIMDCongestionControl(socketMock, 50, 576, 1, 50, 5);
        }

        [Test]
        public void TestWindowIncreaseDecrease()
        {
            GenericPacketMock packet1 = new GenericPacketMock(1);
            GenericPacketMock packet2 = new GenericPacketMock(2);
            GenericPacketMock packet3 = new GenericPacketMock(3);
            GenericPacketMock packet4 = new GenericPacketMock(4);
            GenericPacketMock packet5 = new GenericPacketMock(5);
            GenericPacketMock packet6 = new GenericPacketMock(6);
            GenericPacketMock packet7 = new GenericPacketMock(7);

            Assert.IsTrue(this.congestion.CongestionWindowSize == 1);
            this.congestion.SendPacket(packet1);
            this.congestion.Acked(packet1.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 2);
            this.congestion.SendPacket(packet1);
            this.congestion.Acked(packet1.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 3);
            this.congestion.SendPacket(packet2);
            this.congestion.Acked(packet2.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 4);
            this.congestion.SendPacket(packet3);
            this.congestion.Acked(packet3.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 5);
            this.congestion.SendPacket(packet4);
            this.congestion.Acked(packet4.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 5);
            this.congestion.SendPacket(packet5);
            this.congestion.Acked(packet5.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 5);
            this.congestion.SendPacket(packet6);
            this.congestion.PacketDropped(3, 250);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 2);
            this.congestion.SendPacket(packet7);
            this.congestion.Acked(packet7.Seq);
            Assert.IsTrue(this.congestion.CongestionWindowSize == 3, "Congestion window size should be 3");
            
        }
        [Test]
		public void TestReliablityAtomic(){
            base.SetupBaseTest ();
            UInt16 interval = 20;
            UInt16 dgramSize = 576;

            AIMDCongestionControl control1 = new AIMDCongestionControl (base.mPs1, interval, dgramSize);
            AIMDCongestionControl control2 = new AIMDCongestionControl (base.mPs2, interval, dgramSize);

            TunnelCongestionControllerMock t1 = new TunnelCongestionControllerMock (control1, base.mPs1);
            TunnelCongestionControllerMock t2 = new TunnelCongestionControllerMock (control2, base.mPs2);
            base.TestReliablityAtomic (t1, t2, 3 * interval);
        }

		[Test]
		public void TestReliability()
		{
			base.SetupBaseTest ();
			UInt16 interval = 20;
			UInt16 dgramSize = 576;

			TunnelSocketSendIntercept sock1 = new TunnelSocketSendIntercept ();
			TunnelSocketSendIntercept sock2 = new TunnelSocketSendIntercept ();

			AIMDCongestionControl control1 = new AIMDCongestionControl (sock1, interval, dgramSize);
			AIMDCongestionControl control2 = new AIMDCongestionControl (sock2, interval, dgramSize);

			TunnelCongestionControllerMock t1 = new TunnelCongestionControllerMock (control1, sock1);
			TunnelCongestionControllerMock t2 = new TunnelCongestionControllerMock (control2, sock2);

			base.TestReliability (t1, sock1, t2, sock2, 20, dgramSize);
		}
    }
}

