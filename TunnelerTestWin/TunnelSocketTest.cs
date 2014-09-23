using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NUnit.Framework;
using Tunneler;
using Tunneler.Packet;

namespace TunnelerTestWin
{
    [TestFixture]
    class TunnelSocketTest
    {
        TunnelSocket socket;

        [SetUp]
        public void Setup()
        {
            socket = new TunnelSocket();
        }

        [Test]
        public void TestOpenNewTunnelRequest()
        {
            //this actually needs to be done by the tunnel socket itself
            //if an EPK is recieved on a TID that doesn't exist, we should
            //respond by creating a tunnel to receive it 

            TunnelSocketSendIntercept sendIntercept = new TunnelSocketSendIntercept();
            SecureTunnel t = new SecureTunnel(sendIntercept);
            t.ID = 1;
            UInt64 _id = t.ID;
            bool triggered = false;
            EncryptedPacket hello = t.MakeHelloPacket();
            hello.TID = hello.TID |= Common.PUBLIC_KEY_FLAG; //this should be done during packing
            sendIntercept.SetSendInterceptor(p =>
            {
                //socket.HandlePacket ((EncryptedPacket)p);
                Assert.IsTrue(t.ID == (p.TID & ~Common.TID_FLAGS), String.Format("Key was: {0} but was supposed to be {1}", p.TID, t.ID));
                TunnelBase ta;
                Assert.IsFalse(socket.mTunnelDirectory.Get(t.ID, out ta), "Tunnel ID couldn't be found");
                triggered = true;
            });

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 5000);
            sendIntercept.HandlePacket(hello);
            //t.CommunicateWith (ep);
            //t.CommunicateWith (ep);
            Assert.IsTrue(triggered);

            triggered = false;

            //TODO: When a decryption fails due to a packet
            //being sent with an incorrect key we should 
            //add that ip address to a "caution" list. If there 
            //are repeated attepts at opening tunnels or 
            //adding tunnels then we should  challenge with a puzzle
            //or blacklist the user altogether.
        }
    }

    class TunnelSocketSendIntercept : TunnelSocket
    {
        private Action<GenericPacket> sendInterceptor;
        public TunnelSocketSendIntercept()
            : base()
        {

        }

        public override void SendPacket(GenericPacket p)
        {
            if (sendInterceptor != null)
            {
                sendInterceptor.Invoke(p);
            }
            else
            {
                base.SendPacket(p);
            }
        }

        public void SetSendInterceptor(Action<GenericPacket> action)
        {
            this.sendInterceptor = action;
        }

    }
}
