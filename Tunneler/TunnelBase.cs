using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using C5;
using Sodium;
using Tunneler.Comms;
using Tunneler.Packet;
using Tunneler.Pipe;

namespace Tunneler
{
    /// <summary>
    /// A abstractTunnel sends and recievies packets, manages reliablity, congestion. In addition,
    /// a abstractTunnel is responsible for handling incoming connection requests, and managing a 
    /// connection's life cycle. However, a TunnelSocket handles all of the raw communication
    /// and byte munging. As a result Tunnels are bound to TunnelSockets, and the two are strongly
    /// coupled.
    /// 
    /// It's important to understand that a SecureAbstractTunnel is an abstraction on top of a UDP port. There can
    /// be any number of tunnels attached to that udp port and, because udp is a connectionless protocol,
    /// each packet maintains connection information.
    /// </summary>
    public abstract class TunnelBase
    {
        #region Members
        protected TunnelSocket _socket;
        internal CongestionControlBase congestionController;
        #endregion

        public IPEndPoint RemoteEndPoint { get; protected set; }
        public IPEndPoint LocalEndpoint { get; protected set; }

        /// <summary>
        /// The SecureAbstractTunnel's ID
        /// </summary>
        public UInt64 ID { set; get; }

        /// <summary>
        /// The SecureAbstractTunnel's control connection. The control connection is responsible for lifecycle
        /// management on the abstractTunnel.
        /// </summary>
        /// <value>The control connection.</value>
        public ControlPipe ControlPipe { protected set; get; }

        private TreeDictionary<UInt32, PipeBase> _activePipes;
        private GuardedDictionary<UInt32, PipeBase> connections;

        /// <summary>
        /// ActivePipes is a tree based structure that provides fast retrieval of a
        /// connection.
        /// </summary>
        /// <value>The connection Tree.</value>
        protected TreeDictionary<UInt32, PipeBase> ActivePipes
        {
            set
            {
                this._activePipes = value;
                this.connections = new GuardedDictionary<uint, PipeBase>(_activePipes);
            }
            get { return _activePipes; }
        }
        public GuardedDictionary<UInt32, PipeBase> Connections
        {
            get { return connections; }
        }
        /// <summary>
        /// Gets the TunnelSocket that this abstractTunnel is bound to.
        /// </summary>
        /// <value>The abstractTunnel socket.</value>
        public TunnelSocket TunnelSocket { protected set; get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractTunnel"/> class on an 
        /// existing abstractTunnel socket. 
        /// </summary>
        /// <param name="socket">Socket.</param>
        /// <param name="tid">Tid.</param>
        public TunnelBase(TunnelSocket socket, UInt64 tid)
        {
            this._socket = socket;
            this.ID = tid;
            this.ActivePipes = new TreeDictionary<uint, PipeBase>();
            this.congestionController = new NoCongestionControl(_socket, 250, 500, 1, 500);
        }

        public TunnelBase(TunnelSocket socket)
        {
            this._socket = socket;
            this.ID = Common.RemoveTIDFlags(BitConverter.ToUInt64(SodiumCore.GetRandomBytes(8), 0));
            this.ActivePipes = new TreeDictionary<uint, PipeBase>();
            this.congestionController = new NoCongestionControl(_socket, 250, 500, 1, 500);
        }

        /// <summary>
        /// Opens the abstractTunnel to the specified endpoint as well as opening the 
        /// control connection.
        /// </summary>
        /// <param name="remoteEndpoint">Remote endpoint.</param>
        public abstract void CommunicateWith(IPEndPoint remoteEndpoint);

        /// <summary>
        /// Creates a pipe on this side of the abstractTunnel. At this point the ID 0 should've
        /// already done an exchange with the other side of the abstractTunnel and it should only be creating 
        /// an object that will handle communications between 
        /// </summary>
        /// <param name="connection">Connection object</param>
        /// <param name="cid">ID of the connection object</param>
        /// <returns>True if the connection is being handled by the abstractTunnel
        ///          False otherwise</returns>
        public abstract bool OpenPipe(PipeBase connection);

        /// <summary>
        /// Closes the specified connection -- comms should be handled by the control connection already. This simply
        /// removes the handle from the abstractTunnel's lookup tree
        /// </summary>
        /// <param name="id"></param>
        public abstract bool ClosePipe(uint id);

        /// <summary>
        /// Handles an incoming packet by decrypting and it and sending it to the 
        /// approiate connection. Note this is an implementation detail for the
        /// abstractTunnel itself so we shouldn't consider this a public function. Connections
        /// should be the only way a service communicates end-to-end.
        /// </summary>
        /// <param name="p">GenericPacket to be decrypted</param>
        public abstract void HandleIncomingPacket(EncryptedPacket p);

        /// <summary>
        /// Handles an incoming opening packet (a key exchange essentially).
        /// </summary>
        /// <param name="p">P.</param>
        public abstract void HandleHelloPacket(EncryptedPacket p);

        /// <summary>
        /// Sends a packet out to the interweb. Will just send the packet as is,
        /// no expectations of encryption should be made on the part of the abstractTunnel. 
        /// Only connections or the abstractTunnel itself should call this.
        /// </summary>
        /// <param name="p"></param>
        public abstract void SendPacket(GenericPacket p);

        /// <summary>
        /// The abstractTunnel will packetize and send the data
        /// </summary>
        /// <param name="date">Date.</param>
        /// <param name="cid">Connection ID</param>
        internal abstract void SendData(byte[] date, UInt32 cid);

        /// <summary>
        /// Closes the communications -- cleans up the socket all the objects etc..
        /// </summary>
        internal abstract void CloseCommunications();

        /// <summary>
        /// Prepares to set the next TID (initiated on this side).
        /// </summary>
        internal abstract UInt64 NextTID();

        /// <summary>
        /// Sets the identified next tid
        /// </summary>
        /// <param name="tid">TID to be set</param>
        /// <returns>TID</returns>
        internal abstract void NextTID(UInt64 tid);

        /// <summary>
        /// Check to see if the passed id exsits
        /// </summary>
        /// <returns>True if it does, Flase otherwise</returns>
        internal abstract bool PipeIDExists(UInt32 id);

        /// <summary>
        /// Encrypts and sends a packet down the wire. It will also set all of the 
        /// parameters nessecary for comms
        /// </summary>
        /// <param name="packet">GenericPacket to be sent</param>
        public abstract void EncryptAndSendPacket(EncryptedPacket packet);

        public abstract IEnumerable<UInt32> PipeIDs { get; }

        public abstract void RekeyNow();

        public abstract IHasSerializationTag PrepareRekey();

        public abstract void SetNextRecipentPublicKey(byte[] key);

        /// <summary>
        /// Gets the Maximum transmission unit. This has to be LESS the payload size.
        /// It is used by the Pipes themselves to determine how to split 
        /// their data and how to arrange the packets. This is equivalent to the
        /// MSS (Maximum Segment Size) in TCP based systems.
		/// 
		/// This should be handled by the Congestion Control.
        /// </summary>
        /// <returns>The MT.</returns>
        public virtual UInt16 GetMaxPayloadSize()
        {
            //return 1500 - (86 + 20);
			return 576;
        }


    }
}
