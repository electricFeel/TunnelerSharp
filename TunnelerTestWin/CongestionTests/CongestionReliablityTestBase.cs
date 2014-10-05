using System;
using System.Threading;
using NUnit.Framework;
using Tunneler.Packet;
using TunnelerTestWin.mocks;
using Tunneler.Comms;
using Tunneler;
using System.Collections.Generic;
using System.Text;
using C5;



namespace TunnelerTestWin
{
    [TestFixture]
    public abstract class CongestionReliablityTestBase
    {
        internal class PacketSenderMock : IPacketSender{
            internal Action<GenericPacket> handle;
            #region IPacketSender implementation

            public void SendPacket (GenericPacket p)
            {
                if (handle != null)
                    handle.Invoke (p);
            }

            #endregion

            internal void InterceptOutgoingPacket(Action<GenericPacket> packetInterceptor){
                this.handle = packetInterceptor;
            }
        }

        internal PacketSenderMock mPs1;
        internal PacketSenderMock mPs2;

        protected void SetupBaseTest(){
            mPs1 = new PacketSenderMock ();
            mPs2 = new PacketSenderMock ();
        }

        protected void TearDownTest(){
            mPs1 = null;
            mPs2 = null;
        }
        /// <summary>
        /// Tests the reliablity components of the underlying protocol. In order to do that,
        /// we need to simulate sending a number of packets in-order, out of order, in-time
        /// and out of time (to simulate dropped packets). This is an extremly granular test
        /// of the packets themselves, a seperate message reliabilty test exists to ensure
        /// that pipes receive their data and congestion control does its job or resending
        /// unacked packets.
        /// 
        /// The concrete congestion tests will set up two controller mocks and pass them into
        /// this function which will simulate communications between the two points. This function
        /// assumes that all of the objects have been correctly 
        /// </summary>
        /// <param name="ep1">Ep1.</param>
        /// <param name="ep2">Ep2.</param>
        /// <param name="maxTimeoutPerPacket">A maximum timeout per packet 
        /// 								  before packetloss is deemed a failure</param>
        internal void TestReliablityAtomic(TunnelCongestionControllerMock ep1, 
                                           TunnelCongestionControllerMock ep2,
                                           Int32 maxTimeOutPerPacket)
        {
            Assert.IsNotNull (mPs1, "You must set ep1 to use mPs1 as its packet sender");
            Assert.IsNotNull (mPs2, "You must set ep2 to use mPs2 as its packet sender");

            GenericPacketMock p1 = new GenericPacketMock (1);
            GenericPacketMock p2 = new GenericPacketMock (2);
            GenericPacketMock p3 = new GenericPacketMock (3);
            GenericPacketMock p4 = new GenericPacketMock (4);
            Queue<int> expectedAcks = new Queue<int> ();

            for(int i = 1; i <= 4; i++ ){
                expectedAcks.Enqueue (i);
            }

            //the test sends packets from ep1 to ep2
            int curCount = 1;
            mPs1.InterceptOutgoingPacket (p => {
                //ensure that the acks come in order
                ep2.HandleIncomingPacket (p);
            });

            mPs2.InterceptOutgoingPacket ( p => {
                ep1.HandleIncomingPacket (p);
                Assert.IsTrue (expectedAcks.Dequeue () == p.Ack, "Failed at packet seq" + p.Ack);
            });


            ep1.SendPacket (p1);
            ep1.SendPacket (p2);
            ep1.SendPacket (p4);
            ep1.SendPacket (p3);
        }
        
        /// <summary>
        /// This test launches multiple tests and sends a series of packets through an object
        /// that simulates packet loss. The packet loss will medium will randomly simulate 
        /// RTT delays, MTU size differences and just packet loss. 
        /// </summary>
        internal void TestReliability(TunnelCongestionControllerMock ep1,
                                      TunnelSocketSendIntercept sock1,
                                      TunnelCongestionControllerMock ep2,
                                      TunnelSocketSendIntercept sock2,
                                      int lossPercentage,
									  UInt32 dgramSize)
        {
            LossyConnection connection = new LossyConnection(lossPercentage);
            
            //hook ep1 to ep2
            //sending from ep1 to ep2
            sock1.SetSendInterceptor(p =>
                {
                    //outgoing packet needs to be bubbled into sock2
                    connection.SendPacket(p, ep2);
                });

            //ep2 sends an ack to ep1
            sock2.SetSendInterceptor(p =>
                {
                    //add test for acks (they should always come in order)
                    connection.SendPacket(p, ep1);
                });
            int curAck = 1;
            ep1.SetIncomingPacketInterceptor(p =>
                {
                    //should receive Ack's in order
                    Assert.IsTrue(curAck == p.Ack, "Acks should be received in order");
                });
			StringBuilder buildString = new StringBuilder ();
            ep2.SetIncomingPacketInterceptor(p =>
                {
					GenericPacketMock gp = (GenericPacketMock) p;
					buildString.Append (Encoding.ASCII.GetString (gp.GetPayload ()));
                });

            //the test executes on a seperate thread
            Thread testThread = new Thread(new ThreadStart(() =>
                {
					byte[] txt = Encoding.ASCII.GetBytes (this.sendText);
					int size = 576-8-80;
					uint seq = 1;
					ArrayList<GenericPacketMock> packets = new ArrayList<GenericPacketMock>();
					int numPackets = txt.Length / size;
					int overFlow = txt.Length % size;
					if(overFlow > 0) numPackets += 1;
					for(int i = 0; i < numPackets; i++){
						int sizeSubArray = Math.Min (size, txt.Length - (i * size));
						byte[] sub = new byte[sizeSubArray];
						Array.Copy (txt, i*size, sub, 0, sizeSubArray);
						GenericPacketMock p = new GenericPacketMock(seq);
						p.SetPayload (sub);
						seq++;
						packets.Add (p);
					}
					foreach(GenericPacket p in packets){
						ep1.SendPacket (p);
					}
                }));
            testThread.Start();
            Thread.Sleep(1000);
			Assert.IsTrue (this.sendText.Equals (buildString.ToString ()), buildString.ToString ());
        }

        internal class LossyConnection
        {
            internal TunnelCongestionControllerMock ep1;
            internal TunnelCongestionControllerMock ep2;
            internal Random random;
            internal int lossPercentage;
            private bool delayOn = false;
            private int delayLength = 250;

            internal LossyConnection(int targetLossPercentage)
            {
                this.lossPercentage = targetLossPercentage;
                random = new Random();
            }

            internal void SimulateRTTDelay(bool onOff, int delayLength)
            {
                this.delayLength = delayLength;
                this.delayOn = onOff;
            }

            internal bool SendPacket(GenericPacket p, TunnelCongestionControllerMock to)
            {
                if (random.Next(0, 100) < lossPercentage)
                {
                    //drop the packet
					Console.WriteLine ("DROPPING packet Seq#" + p.Seq);
                    return false;
                }
                else
                {
                    //send the packet
                    if (this.delayOn)
                    {
                        Thread.Sleep(delayLength);
                    }
                    to.HandleIncomingPacket(p);
                    return true;
                }
            }
        }

        /// <summary>
        /// A large block of text to test against the lossy congestion control medium.
        /// </summary>
        internal String sendText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                                   "In scelerisque lorem a lacus bibendum porttitor. " +
                                   "Mauris nunc massa, vestibulum in scelerisque nec, " +
                                   "commodo luctus augue. In mollis nec justo et venenatis. " +
                                   "Proin dapibus sapien imperdiet risus vehicula scelerisque. " +
                                   "Nulla sed elit pulvinar, semper diam vitae, mollis mi. " +
                                   "Quisque nec neque quis lectus varius tincidunt. " +
                                   "Nulla sit amet augue tellus. Nulla vel nisi pretium eros euismod luctus. " +
                                   "Cras facilisis nec turpis id gravida. Praesent at orci ante. Duis iaculis, " +
                                   "urna in vulputate imperdiet, purus lectus ullamcorper ex, " +
                                   "sit amet sollicitudin mauris augue vestibulum neque. Ut in congue velit. " +
                                   "Suspendisse potenti. Nam facilisis auctor neque quis sagittis. " +
                                   "Suspendisse potenti.In euismod ipsum vitae felis commodo interdum. " +
                                   "Curabitur cursus eu quam id mattis. Nulla nec urna et lorem vulputate elementum. " +
                                   "Proin iaculis eu eros non posuere. Nam accumsan eget massa vitae pharetra. " +
                                   "Pellentesque eleifend risus urna, sit amet dapibus lectus faucibus ac. Ut eros arcu, " +
                                   "dapibus a diam id, placerat porta ipsum. In hac habitasse platea dictumst. " +
                                   "In fringilla elit massa, sollicitudin porttitor nisi imperdiet pretium. In imperdiet dui " +
                                   "in diam feugiat bibendum. Donec vel lectus ornare, vulputate quam vitae, feugiat metus. " +
                                   "Vestibulum elementum turpis eleifend, egestas nunc vel, lobortis mi. Phasellus aliquet felis" +
                                   " vel dui sollicitudin gravida. Fusce sagittis ante lacus. Integer ultricies fermentum suscipit.Nunc " +
                                   "vitae nulla nec lectus vehicula commodo. Quisque maximus eros vitae pellentesque interdum. Morbi " +
                                   "euismod dui posuere metus imperdiet vestibulum. Vivamus vehicula finibus justo, sit amet ullamcorper" +
                                   " quam blandit in. Praesent eu mauris non justo pharetra molestie eget consequat nisi. Nam at tempor" +
                                   " ex. Morbi id molestie metus. In auctor sem quis nisl aliquam, at placerat lacus dignissim. Aliquam " +
                                   "luctus vitae tortor sed consectetur. Duis ultrices nisl maximus neque cursus rhoncus. Duis euismod, " +
                                   "nisi non ornare blandit, massa felis bibendum urna, id congue lectus diam non urna. Suspendisse " +
                                   "blandit nunc sit amet dignissim tristique.Pellentesque sed arcu id est suscipit blandit et in" +
                                   " turpis. Aenean fermentum risus a risus tempus blandit. Fusce faucibus eget erat tristique fringilla. " +
                                   "Donec vulputate dapibus sagittis. Aliquam gravida ex ultrices bibendum molestie. In facilisis, " +
                                   "turpis eget sagittis accumsan, elit risus aliquam ante, at pharetra felis tortor et elit. Nullam " +
                                   "porta augue id nunc facilisis, ut tincidunt tortor finibus. Suspendisse potenti. Praesent viverra " +
                                   "vulputate lorem, eu pharetra ipsum dignissim vel. Nunc feugiat magna eu odio vulputate ullamcorper." +
                                   "Interdum et malesuada fames ac ante ipsum primis in faucibus. Aenean nec placerat quam. Integer " +
                                   "sodales augue at odio ullamcorper, nec tempus dolor rutrum. Cras in lacinia neque. Ut vitae congue " +
                                   "sem. Nulla sit amet molestie ipsum. Integer in orci quam. Duis tempor metus nec pharetra gravida. " +
                                   "Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Aliquam vitae " +
                                   "nisl id sem dignissim auctor sit amet ac eros. Nullam pharetra, velit eu volutpat ultrices, urna erat" +
                                   " dignissim nisi, vel finibus urna ipsum in eros. Donec vel feugiat ex, vitae semper risus.";
    }
}

