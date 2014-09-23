using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Tunneler;
using Tunneler.Pipe;

namespace TunnelerTestWin
{
    [TestFixture()]
    public class EndToEndTunnelTests
    {
        [SetUp()]
        public void Setup() { }

        [Test()]
        public void TestClientServer()
        {
            SecureTunnel st = new SecureTunnel(10000);
            st.ID = 1;
            TunnelSocket ts = TunnelRuntime.GetOrCreateTunnelSocket(10001);	//this should create a tunnel 
            //to act as a server
            st.CommunicateWith(ts.LocalEndPoint);
            Thread.Sleep(350);
            UInt32 cid = 1000;
            DuplexPipe c = (DuplexPipe)st.ControlPipe.OpenNewPipe(PipeType.Duplex, cid);
            Thread.Sleep(350);

            IList<UInt64> ids = ts.mTunnelDirectory.GetIDs();
            Assert.IsTrue(ids.Count > 0);
            Assert.IsTrue(st.ID == ids.First());

            TunnelBase createdTunnel;
            Assert.True(ts.mTunnelDirectory.Get(ids.First(), out createdTunnel));

            Assert.True(createdTunnel.PipeIDs.Contains(cid), "Tunnel should've had a duplex connection created");

            String testMsg = "This is a basic message of greater the 50 charachters length to test the " +
                             "the splitting and reforming of a message.";
            PipeBase c2;
            Assert.IsTrue(createdTunnel.Connections.Find(ref cid, out c2));
            Assert.IsNotNull(c2);
            bool trigger1 = false;
            c.DataReceived += (sender, args) =>
            {
                var ret = System.Text.Encoding.ASCII.GetString(args.Data);
                Assert.AreEqual(testMsg, ret);
                trigger1 = true;

            };
            ((DuplexPipe)c2).Send(testMsg);
            Thread.Sleep(250);
            Assert.IsTrue(trigger1, "Message never received");
        }
    }
}
