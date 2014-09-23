using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Tunneler.Pipe;
using TunnelerTestWin.mocks;

namespace TunnelerTestWin
{
    [TestFixture()]
    public class DuplexPipeTest
    {
        [SetUp()]
        public void Setup()
        {

        }

        [Test]
        public void RoundSimpleTripTest()
        {
            TunnelMock tunnel1 = new TunnelMock(new TunnelSocketMock());
            TunnelMock tunnel2 = new TunnelMock(new TunnelSocketMock());
            int packetSendCount = 0;
            bool trigger1, trigger2;
            trigger1 = trigger2 = false;
            DuplexPipe pipe = new DuplexPipe(tunnel1, 100);
            DuplexPipe pipe2 = new DuplexPipe(tunnel2, 100);


            tunnel1.PacketInterceptor(p =>
            {
                packetSendCount++;
                //determine why tunnel 2 isn't raising the incoking packet to connection2
                pipe2.HandlePacket(p);
                trigger1 = true;
            });

            var msg = "This is a basic message of greater the 50 charachters length to test the " +
                       "the splitting and reforming of a message.";


            pipe2.DataReceived += (object sender, DataReceivedEventArgs args) =>
            {
                var ret = System.Text.Encoding.ASCII.GetString(args.Data);
                Assert.AreEqual(msg, ret);
                trigger2 = true;
            };

            tunnel1.SetMTUSize(50);
            tunnel2.SetMTUSize(50);

            //this should form 3 packets
            pipe.Send(msg);

			Assert.IsTrue(packetSendCount == 3);
            Assert.IsTrue(trigger1);
            Assert.IsTrue(trigger2);
        }
    }
}
