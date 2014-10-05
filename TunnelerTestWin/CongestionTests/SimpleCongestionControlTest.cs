using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Tunneler.Comms;
using TunnelerTestWin.mocks;

namespace TunnelerTestWin.CongestionTests
{
    [TestFixture]
    class SimpleCongestionControlTest
    {
        private TunnelSocketMock mockSock;
        private SimpleCongestionControl congestion;
        [SetUp]
        public void SetUp()
        {
            mockSock = new TunnelSocketMock();
            congestion = new SimpleCongestionControl(mockSock, 250, 500, 1, 500);
        }

        /// <summary>
        /// No matter what the congestion window should be 1 but if an ack isn't received then
        /// the send queue should begin to increase
        /// </summary>
        [Test]
        public void TestCongestion()
        {
            GenericPacketMock packet1 = new GenericPacketMock(1);
            GenericPacketMock packet2 = new GenericPacketMock(2);
            GenericPacketMock packet3 = new GenericPacketMock(3);
            GenericPacketMock packet4 = new GenericPacketMock(4);
            GenericPacketMock packet5 = new GenericPacketMock(5);
            GenericPacketMock packet6 = new GenericPacketMock(6);
            GenericPacketMock packet7 = new GenericPacketMock(7);

			mockSock.InterceptIncomingPacket((Tunneler.Packet.GenericPacket p) => {
			});
            this.congestion.SendPacket(packet1);
        }
    }
}
