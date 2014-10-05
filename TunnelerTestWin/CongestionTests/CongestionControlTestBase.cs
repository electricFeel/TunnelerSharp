using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Tunneler.Comms;
using Tunneler.Packet;
using TunnelerTestWin.mocks;
namespace TunnelerTestWin.CongestionTests
{

    /// <summary>
    /// Contains the common tests for all the congestion control libraries including
    /// basic reliabity tests
    /// </summary
    public abstract class CongestionControlTestBase
    {
        internal TunnelMock tunnel;
        internal TunnelSocketMock socketMock1;
        internal TunnelSocketMock socketMock2;

        private CongestionControlBase congestionControlBase;
        
        protected void SetupMembers()
        {
            this.socketMock1 = new TunnelSocketMock();
            this.socketMock2 = new TunnelSocketMock();
            this.tunnel = new TunnelMock(this.socketMock1);
            this.congestionControlBase = null;
            socketMock1.InterceptOutgoingPacket(packet => socketMock2.HandlePacket(packet));
        }
        

        internal void TestReliablity(CongestionControlBase congestionControl)
        {
            SetupMembers();
            this.congestionControlBase = congestionControl;
            Assert.IsNotNull(congestionControlBase, "You must instantiate the congestion control base before running tests");

            GenericPacketMock p1 = new GenericPacketMock(1);
            GenericPacketMock p2 = new GenericPacketMock(2);
            GenericPacketMock p3 = new GenericPacketMock(3); 
            GenericPacketMock p4 = new GenericPacketMock(4);
            GenericPacketMock p5 = new GenericPacketMock(5);
            GenericPacketMock p6 = new GenericPacketMock(6);

            UInt16 expectedSeq = 1;
            bool triggered = false;
            socketMock2.InterceptIncomingPacket(packet =>
                {
                    Assert.IsTrue(packet.Seq == expectedSeq, String.Format("Expected packet with seq {0} and got {1} instead", expectedSeq, packet.Seq));
                    triggered = true;
                    GenericPacket ackPacket = new GenericPacketMock(0);
                    ackPacket.Ack = packet.Seq;
                });
            
            tunnel.SendPacket(p1);

            Assert.IsTrue(triggered);
        }
    }
}
