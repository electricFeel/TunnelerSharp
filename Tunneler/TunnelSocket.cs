using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tunneler.Packet;

namespace Tunneler
{
    public interface IPacketSender
    {
        /// <summary>
        /// Enqueues the send GenericPacket on the send queue.
        /// </summary>
        /// <param name="p">P.</param>
        void SendPacket(GenericPacket p);
    }

    /// <summary>
    /// A TunnelSocket takes care of accepting new connection from a abstractTunnel or pushing 
    /// data to already established tunnels. In this role it abstracts away the specifics 
    /// of dealing with the socket. Datagram managment is still upto the SecureAbstractTunnel itself,
    /// but sending and recieving, accepting new tunnels and basic authentication happens 
    /// at this level.
    /// 
    /// Each abstractTunnel instance runs in its own thread. 
    /// </summary>
    public class TunnelSocket : IPacketSender
    {
        //private static Logger logger = LogManager.GetCurrentClassLogger();

        //locks
        protected ReaderWriterLockSlim port_lock = new ReaderWriterLockSlim();
        //private ReaderWriterLockSlim recv_window_lock = new ReaderWriterLockSlim ();

        //autoreset events
        protected AutoResetEvent readerEvent = new AutoResetEvent(false);
        protected AutoResetEvent icmpEvent = new AutoResetEvent (false);

        protected short port;
        protected bool isOn = false;

        //private Socket outputSocket;
        protected Socket socket;
        //protected Socket icmp;

        //this needs to be moved to the abstractTunnel
        protected UInt16 mtu = 2048;

        /// <summary>
        /// Local endpoint
        /// </summary>
        protected IPEndPoint ep;
        /// <summary>
        /// Thread that handles recieving data
        /// </summary>
        protected Thread receiverThread;
        /// <summary>
        /// Internal abstractTunnel directory for passing packets to.
        /// </summary>
        internal TunnelDirectory mTunnelDirectory = new TunnelDirectory();

        public IPEndPoint LocalEndPoint
        {
            get
            {
                return this.ep;
            }
        }

        /// <summary>
        /// Port thats associated with this abstractTunnel.
        /// </summary>
        /// <value>The port.</value>
        public short Port
        {
            get
            {
                port_lock.EnterReadLock();
                short _port = this.port;
                port_lock.ExitReadLock();
                return _port;
            }
            protected set
            {
                port_lock.EnterWriteLock();
                port = value;
                port_lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Default constructor should only be used for testing
        /// </summary>
        public TunnelSocket()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelSocket"/> class.
        /// </summary>
        /// <param name="port">Port number</param>
        public TunnelSocket(short port)
        {
            //logger.Debug(String.Format("Creating a new tunnel socket on port: {0}", port));
            //convert this ip endpoint setup to use the DNS resolver
            this.Port = port;
        }

        /// <summary>
        /// Registers the passed abstractTunnel to the socket. Should return true but in extremly rare cases
        /// where the TID is already taken, it may return false. 
        /// </summary>
        /// <returns><c>true</c>, if abstractTunnel was registered, <c>false</c> otherwise.</returns>
        /// <param name="abstractTunnel">SecureAbstractTunnel.</param>
        public bool RegisterTunnel(TunnelBase abstractTunnel)
        {
            if (this.mTunnelDirectory.TunnelIDExists(abstractTunnel.ID))
            {
                return false;
            }
            this.mTunnelDirectory.InsertTunnel(abstractTunnel);
            return true;
        }

        /*internal bool RegisterTunnelRekey(AbstractTunnel abstractTunnel, UInt64 tid){
            if(this.mTunnelDirectory.TunnelIDExists (tid)){
                return false;
            }
            this.mTunnelDirectory.InsertTunnel (abstractTunnel, tid);
            return true;
        }*/

        /// <summary>
        /// Unregisters abstractTunnel. 
        /// </summary>
        /// <returns><c>true</c>, if register abstractTunnel was uned, <c>false</c> otherwise.</returns>
        /// <param name="abstractTunnel">SecureAbstractTunnel.</param>
        internal bool UnRegisterTunnel(TunnelBase abstractTunnel)
        {
            if (this.mTunnelDirectory.TunnelIDExists(abstractTunnel.ID))
            {
                this.mTunnelDirectory.RemoveTunnel(abstractTunnel.ID);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Initializes the socket and starts the SecureAbstractTunnel Socket on a new thread
        /// </summary>
        public virtual void Start()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //icmp = new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            //this.socket.DontFragment = true;
            ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            socket.Bind(ep);
            //icmp.Bind (ep);

            isOn = true;
            ThreadStart start = new ThreadStart(StartRecievingUDPDatagrams);
            receiverThread = new Thread(start);
            receiverThread.Start();
        }

        /// <summary>
        /// Closes the tunnels, cleans up the threads and frees the socket.
        /// </summary>
        public virtual void Close()
        {
            this.mTunnelDirectory.CloseAllTunnels();
            this.socket.Close();
            this.readerEvent.Close();
            this.readerEvent.Dispose();
            if (receiverThread != null)
            {
                this.receiverThread.Join();
            }
        }

        #region Handle Packets

        /// <summary>
        /// Enqueues the send GenericPacket on the send queue.
        /// </summary>
        /// <param name="p">P.</param>
        public virtual void SendPacket(GenericPacket p)
        {
            byte[] buffer = p.ToBytes();
            
            this.socket.BeginSendTo(buffer,
                    0,
                    buffer.Length,
                    SocketFlags.None,
                    p.destination,
                    new AsyncCallback(OnPacketSent),
                    p);

            /*this.socket.BeginSend (buffer, 
                                        0, 
                                        buffer.Length,
                                        SocketFlags.None,
                                        new AsyncCallback (OnPacketSent),
                                        p);*/
        }

        protected void OnPacketSent(IAsyncResult ar)
        {
            Console.WriteLine("GenericPacket sent....");
        }

        private void StartRecievingICMPDatagrams()
        {
            while(this.isOn)
            {
                icmpEvent.Reset ();
                byte[] buffer = new byte[4096];
            }
        }

        private void StartRecievingUDPDatagrams()
        {
            while (this.isOn)
            {
                readerEvent.Reset();
                EncryptedPacket p = new EncryptedPacket(mtu);
                p.sender = (EndPoint)ep;
                socket.BeginReceiveFrom(p.rawBytes,
                                              0,
                                              p.rawBytes.Length,
                                              SocketFlags.None,
                                              ref p.sender,
                                              new AsyncCallback(OnPacketRecieved),
                                              p);
                readerEvent.WaitOne();
            }
        }

        private void OnPacketRecieved(IAsyncResult ar)
        {
            EncryptedPacket p = (EncryptedPacket)ar.AsyncState;
            p.TruncateRaw((UInt16)socket.EndReceiveFrom(ar, ref p.sender));
            ThreadPool.QueueUserWorkItem(new WaitCallback(HandlePacket), p);
            readerEvent.Set();
        }

        internal void HandlePacket(EncryptedPacket packet)
        {
            Console.WriteLine(String.Format("GenericPacket received {0}", Encoding.UTF8.GetString(packet.rawBytes, 0, packet.rawBytes.Length)));
            TunnelBase t = null;
            if (this.mTunnelDirectory.Get(packet.TID, out t))
            {
                t.HandleIncomingPacket(packet);
            }
            else if (packet.HasEPK)
            {
                //open a new abstractTunnel
                //todo: we need some way of specifying which abstractTunnel type we're creating
                //to be using here.;
                t = new SecureTunnel(this);
                t.ID = packet.TID;
                this.RegisterTunnel(t);
                t.HandleHelloPacket(packet);
            }
        }

        private void HandlePacket(object raw)
        {
            EncryptedPacket packet = (EncryptedPacket)raw;
            packet.UnpackHeader();
            HandlePacket(packet);
        }
        #endregion
    }
}
