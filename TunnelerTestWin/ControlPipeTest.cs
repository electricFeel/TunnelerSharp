using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Sodium;
using Tunneler.Packet;
using Tunneler.Pipe;
using TunnelerTestWin.mocks;

namespace TunnelerTestWin
{
    [TestFixture()]
    public class ControlPipeTest
    {
        private TunnelMock tunnel;
        private TunnelSocketMock tunnelSock;

        [SetUp()]
        public void Setup()
        {
            tunnel = new TunnelMock(new TunnelSocketMock());
        }
        [Test()]
        public void CreateControl()
        {
            ControlPipe c = new ControlPipe(tunnel);
            Assert.That(c.ID == 0);
        }

        [Test]
        public void HandleOpenPipeRequest()
        {
            //tests that the control pipe both opens a new pipe and sends an ack
            bool trigger1, trigger2;
            trigger1 = trigger2 = false;
            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count >= 1);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.AckPipe);
                trigger1 = true;
            });

            tunnel.PipeInterceptor(connection =>
            {
                Assert.That(connection.ID == 100);
                trigger2 = true;
            });
            ControlPipe c = new ControlPipe(tunnel);
            EncryptedPacket packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(new CreateAnonymousPipe(PipeType.Duplex.ToString(), 100));
            c.HandlePacket(packet);

            Assert.IsTrue(trigger1 && trigger2, "One of the triggers was not called");
        }

        [Test]
        public void HandleRefuseControlPipe()
        {
            var createAnonymousPipe = new CreateAnonymousPipe(PipeType.Duplex.ToString(), 0);
            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count >= 1);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.RefusePipe);
                RefusePipe req = (RefusePipe)p.RPCs.First;
                Assert.That(req.Reason == (byte)RefusePipe.RefusalReason.CANNOT_OPEN_ANOTHER_CONTROL);
            });

            tunnel.PipeInterceptor(connection =>
            {
                Assert.Fail("Connection request should never hit the tunnel");
            });
            ControlPipe c = new ControlPipe(tunnel);
            EncryptedPacket packet = new EncryptedPacket(tunnel.ID, 0);

            packet.RPCs.Add(createAnonymousPipe);
            c.HandlePacket(packet);
        }

        [Test]
        public void TestRefuseCreatingSameCID()
        {
            ClosePipeRPC close = new ClosePipeRPC(100);
            uint requestId = close.RequestID;
            //create the connection
            ControlPipe c = new ControlPipe(tunnel);
            EncryptedPacket packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(new CreateAnonymousPipe(PipeType.Duplex.ToString(), 100));
            c.HandlePacket(packet);

            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count > 0);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.RefusePipe);
                RefusePipe refuse = (RefusePipe)p.RPCs.First;
                Assert.IsTrue(refuse.Reason == (byte)RefusePipe.RefusalReason.ID_ALREADY_EXISTS);
            });

            tunnel.PipeInterceptor(connection =>
            {
                Assert.Fail("Connection request should never hit the tunnel");
            });

            packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(new CreateAnonymousPipe(PipeType.Duplex.ToString(), 100));
            c.HandlePacket(packet);

        }

        [Test]
        public void HandleClosePipeRequest()
        {
            ClosePipeRPC close = new ClosePipeRPC(100);
            uint requestId = close.RequestID;
            //create the connection
            ControlPipe c = new ControlPipe(tunnel);
            EncryptedPacket packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(new CreateAnonymousPipe(PipeType.Duplex.ToString(), 100));
            c.HandlePacket(packet);

            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count > 0);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.Ok);
                OkRPC ok = (OkRPC)p.RPCs.First;
                Assert.IsTrue(ok.RPCID == requestId);
            });

            tunnel.PipeInterceptor(connection =>
            {
                Assert.That(connection.ID == 100);
            });
            packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(close);
            c.HandlePacket(packet);
        }

        [Test]
        public void HandlePrepareRekeyRequest()
        {
            byte[] fake = new byte[] { 1, 1, 1 };
            PrepareRekey rekey = new PrepareRekey(fake);
            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count > 0);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.RekeyResponse);
                RekeyResponse ok = (RekeyResponse)p.RPCs.First;
                Assert.IsTrue(ok.NextPublicKey.Length == 32);
            });

            tunnel.RekeyInterceptor(k =>
            {
                Assert.That(k[0] == fake[0]);
                Assert.That(k[1] == fake[1]);
                Assert.That(k[2] == fake[2]);
            });
            ControlPipe c = new ControlPipe(tunnel);
            EncryptedPacket packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(rekey);
            c.HandlePacket(packet);
        }

        [Test]
        public void HandleRekey()
        {
            //A rekey cannot happen until a prepare rekey rpc has been sent (note that they can be send together)
            //todo: add a test to send a prepare rekey and rekey together
            bool trigger1, trigger2, trigger3;
            trigger1 = trigger2 = trigger3 = false;
            KeyPair pair = Sodium.PublicKeyBox.GenerateKeyPair();
            PrepareRekey prepareRekey = new PrepareRekey(pair.PublicKey);
            RekeyNow rekey = new RekeyNow();
            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count > 0);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.Refuse);
                Refuse rpc = (Refuse)p.RPCs.First;
                trigger1 = true;
            });
            ControlPipe c = new ControlPipe(tunnel);
            EncryptedPacket packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(rekey);
            c.HandlePacket(packet);
            Assert.IsTrue(trigger1, "Refuse block never called");

            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count > 0);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.RekeyResponse);
                RekeyResponse ok = (RekeyResponse)p.RPCs.First;
                Assert.IsTrue(ok.NextPublicKey.Length == 32);
                trigger2 = true;
            });


            packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(new PrepareRekey(new byte[] { 1, 2, 3 }));
            c.HandlePacket(packet);
            Assert.IsTrue(trigger2, "Rekey ack block never called");

            tunnel.PacketInterceptor(p =>
            {
                Assert.That(p.RPCs.Count > 0);
                Assert.That(p.RPCs.First.SerializationTag == (byte)RPCType.Ok);
                OkRPC ok = (OkRPC)p.RPCs.First;
                Assert.IsTrue(ok.RPCID == rekey.RequestID);
                trigger3 = true;
            });

            packet = new EncryptedPacket(tunnel.ID, 0);
            packet.RPCs.Add(rekey);
            c.HandlePacket(packet);



            Assert.IsTrue(trigger1, "Rekey now block never called");
			Assert.IsTrue (trigger3);
        }

        [Test]
        public void TestCloseTunnel()
        {
            ControlPipe c = new ControlPipe(tunnel);
            c.CloseTunnel();
        }
    }
}
