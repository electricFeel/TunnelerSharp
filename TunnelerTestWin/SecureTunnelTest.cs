using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NUnit.Framework;
using Sodium;
using Tunneler;
using Tunneler.Packet;
using Tunneler.Pipe;
using TunnelerTestWin.mocks;

namespace TunnelerTestWin
{
    [TestFixture]
    class SecureTunnelTest
    {
        TunnelSocketMock tunnelSocket;
        [SetUp]
        public void Setup()
        {
            tunnelSocket = new TunnelSocketMock();
        }

        [Test]
        public void CreateTunnelTest()
        {
            SecureTunnel t = new SecureTunnel(tunnelSocket);
            Assert.IsNotNull(t);
        }

        [Test]
        public void SendHelloTest()
        {
            bool trigger1 = false;
            tunnelSocket.InterceptPacket(p =>
            {
                trigger1 = true;
                Assert.IsTrue(p.HasEPK);
            });

            SecureTunnel t = new SecureTunnel(tunnelSocket);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 5000);
            t.CommunicateWith(endpoint);
            Assert.IsTrue(trigger1);
        }

        [Test]
        public void TestSendEncryptedPacket()
        {
            SecureTunnel t = new SecureTunnel(this.tunnelSocket);
            bool triggered = false;
            byte[] server_epk = new byte[0];
            this.tunnelSocket.InterceptPacket(p =>
            {
                Assert.IsNotNull(p.EuphemeralPublicKey);
                Assert.IsTrue(p.EuphemeralPublicKey.Length == 32);
                Assert.IsTrue(p.HasEPK);
                triggered = true;
                server_epk = p.EuphemeralPublicKey;
            });
            String messageText = "The quick brown fox jumps over the lazy dog";
            //create an encryption key
            KeyPair keyPair = Sodium.PublicKeyBox.GenerateKeyPair();
            EncryptedPacket hello = new EncryptedPacket(1000, 0);
            hello.EuphemeralPublicKey = keyPair.PublicKey;
            t.HandleHelloPacket(hello);
            Assert.IsTrue(triggered);
            //these are just to ensure that the keys have been exchanged correctly. 
            //We may want the response to a hello to be an encrypted rpc just for added protection -- there is no
            //need for the EPK to be transferred in the clear at this point.
            Assert.That(keyPair.PublicKey.SequenceEqual(t.recipentEPK));
            Assert.That(!keyPair.PublicKey.SequenceEqual(server_epk));
            Assert.That(t.mKeyPair.PublicKey.SequenceEqual(server_epk));
            triggered = false;

            this.tunnelSocket.InterceptPacket(p =>
            {
                EncryptedPacket packet = (EncryptedPacket)p;
                Assert.IsNotNull(packet.ToBytes());
                if (packet.DecryptPacket(keyPair.PrivateKey, server_epk))
                {
                    String msg = System.Text.ASCIIEncoding.ASCII.GetString(packet.Payload);
                    System.Console.WriteLine(msg);
                    //Assert.IsTrue(msg.Equals (messageText));
                    triggered = true;
                }
                else
                {
                    Assert.Fail("Decryption failed");
                }
            });

            EncryptedPacket sentPacket = new EncryptedPacket(1000, 0);
            sentPacket.SetPayload(messageText);
            t.EncryptAndSendPacket(sentPacket);
            Assert.IsTrue(triggered);
        }

        [Test]
        public void TestOpenConnection()
        {
            SecureTunnel t = new SecureTunnel(tunnelSocket);
            DuplexPipe connection = new DuplexPipe(t, 0);
            Assert.IsFalse(t.OpenPipe(connection), "Shouldn't have been able to assign 0 to the abstract connection");

            connection = new DuplexPipe(t, 100);
            Assert.IsTrue(t.OpenPipe(connection), "Should've been able to assign a new connection with ID of 100");

            Assert.IsTrue(t.PipeIDs.Contains((uint)100));

            connection = new DuplexPipe(t, 100);
            Assert.IsFalse(t.OpenPipe(connection), "Shouldn't have been able to create a duplicate connection with the same ID");

        }

        [Test]
        public void TestCloseConnection()
        {
            SecureTunnel t = new SecureTunnel(tunnelSocket);
            byte[] privateKey, publicKey;
            privateKey = new byte[0];
            publicKey = new byte[0];
            this.SetupTunnelComms(t, out privateKey, out publicKey);

            tunnelSocket.InterceptPacket(p =>
            {
                //we should recieve a close connection packet
                EncryptedPacket packet = (EncryptedPacket)p;
                packet.DecryptPacket(privateKey, publicKey);
                var x = packet.RPCs.First;
                Assert.IsTrue(x.SerializationTag == (byte)RPCType.ClosePipe);
            });

            DuplexPipe connection = new DuplexPipe(t, 100);

            Assert.IsTrue(t.OpenPipe(connection));
            Assert.IsTrue(t.PipeIDs.Contains((uint)100));

            Assert.IsTrue(t.ClosePipe((uint)100));
            Assert.IsFalse(t.PipeIDs.Contains((uint)100));
        }

        private void SetupTunnelComms(SecureTunnel tunnel, out byte[] privateKey, out byte[] publicKey)
        {
            byte[] server_epk = new byte[0];
            this.tunnelSocket.InterceptPacket(p =>
            {
                Assert.IsNotNull(p.EuphemeralPublicKey);
                Assert.IsTrue(p.EuphemeralPublicKey.Length == 32);
                Assert.IsTrue(p.HasEPK);
                server_epk = p.EuphemeralPublicKey;
            });
            String messageText = "The quick brown fox jumps over the lazy dog";
            //create an encryption key
            KeyPair keyPair = Sodium.PublicKeyBox.GenerateKeyPair();
            EncryptedPacket hello = new EncryptedPacket(1000, 0);
            hello.EuphemeralPublicKey = keyPair.PublicKey;
            tunnel.HandleHelloPacket(hello);
            privateKey = keyPair.PrivateKey;
            publicKey = tunnel.mKeyPair.PublicKey;
        }
    }
}
