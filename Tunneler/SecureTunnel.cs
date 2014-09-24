using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using Sodium;
using Tunneler.Packet;
using Tunneler.Pipe;

namespace Tunneler
{
    /// <summary>
    ///     A base line abstractTunnel implementation.
    /// </summary>
    public class SecureTunnel : TunnelBase
    {
        private TunnelState _state = TunnelState.Initial;
        private Queue<EncryptedPacket> bufferedEncryptedPackets = new Queue<EncryptedPacket>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Tunnel.SecureTunnel"/> class on the
        /// specified port. 
        /// </summary>
        /// <param name="port">Port.</param>
        public SecureTunnel(short port)
            : base(TunnelRuntime.GetOrCreateTunnelSocket(port))
        {
            State = TunnelState.Starting;
            State = TunnelState.Disconnected;
            this.TunnelSocket = base._socket;
            base._socket.RegisterTunnel(this);
            this.ControlPipe = new ControlPipe(this);
            mCurNonce = SodiumCore.GetRandomBytes(24);
        }

        public SecureTunnel(TunnelSocket socket)
            : base(socket)
        {
            State = TunnelState.Starting;
            //random TID
            ID = Common.RemoveTIDFlags(BitConverter.ToUInt64(SodiumCore.GetRandomBytes(8), 0));
            TunnelSocket = socket;
            State = TunnelState.Disconnected;
            this.ControlPipe = new ControlPipe(this);
            mCurNonce = SodiumCore.GetRandomBytes(24);
        }

        private TunnelState State
        {
            get { return _state; }
            set
            {
                debug(String.Format("changing state from {0} to {1}", _state, value));
                _state = value;
            }
        }

        private void debug(String msg)
        {
            Debug.WriteLine(String.Format("TunnelSocket[{0}] {1}", ID, msg));
        }

        public override void CommunicateWith(IPEndPoint remoteEndpoint)
        {
            if (State == TunnelState.Disconnected)
            {
                State = TunnelState.SendingHello;
                RemoteEndPoint = remoteEndpoint;
                _socket.RegisterTunnel(this);
                SendPacket(MakeHelloPacket());
                State = TunnelState.WaitingForHelloResponse;
            }
            else
            {
                Debug.Fail("Cannot call \"CommunicateWith\" when out of the disconnected state");
            }
        }

        /// <summary>
        /// The control connection handles 
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public override bool OpenPipe(PipeBase connection)
        {
            if (connection.ID == 0)
                return false;

            //first check if the connection already exists
            if (this.PipeIDExists(connection.ID))
                return false;

            this.ActivePipes.Add(connection.ID, connection);
            return true;
        }

        public override bool ClosePipe(UInt32 id)
        {
            PipeBase c;

            if (ActivePipes.Remove(id, out c))
            {
                var p = new EncryptedPacket(ID, GetNextSeq(), c.ID, 0,
                                            RemoteEndPoint, new byte[0]);
                p.RPCs.Add(new ClosePipeRPC(id));

                EncryptAndSendPacket(p);
                return true;
            }
            //connection was either already closed or never existed.
            return false;
        }

        public PipeBase GetConnection(UInt32 cid)
        {
            PipeBase connection;
            if (ActivePipes.Find(ref cid, out connection))
            {
                return connection;
            }
            throw new ArgumentException("ID does not match any existing connection");
        }

        internal override void NextTID(ulong tid)
        {
            this.mNextTID = tid;
        }

        internal override bool PipeIDExists(uint id)
        {
            if (this.ActivePipes.Exists(pair =>
            {
                if (pair.Key == id)
                {
                    return true;
                }
                return false;
            }))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes a GenericPacket, encrypts it and sends it down the pipe with the right Nonce, Seq and Encryption
        /// </summary>
        /// <param name="p"></param>
        public override void EncryptAndSendPacket(EncryptedPacket p)
        {
            if (this.State == TunnelState.WaitingForHelloResponse)
            {
                //we need to enqueue the GenericPacket until we're ready to send
                this.bufferedEncryptedPackets.Enqueue(p);
            }
            else
            {
                p.Nonce = GetNextNonce();
                p.Seq = this.GetNextSeq();
                p.EncryptPacket(mKeyPair.PrivateKey, recipentEPK);
                SendPacket(p);
            }
        }

        public override IEnumerable<uint> PipeIDs
        {
            get { return this.ActivePipes.Keys; }
        }

        public override void RekeyNow()
        {
            if (this.recipentNextEPK != null)
            {
                this.mKeyPair = this.mNextKeyPair;
                this.recipentEPK = this.recipentNextEPK;
                this.mNextKeyPair = null;
                this.recipentNextEPK = null;
            }
            else
            {
                throw new Exception("Cannot rekey until prepare rekey has been called");
            }
        }

        public override IHasSerializationTag PrepareRekey()
        {
            this.mNextKeyPair = Sodium.PublicKeyBox.GenerateKeyPair();
            return new PrepareRekey(this.mNextKeyPair.PublicKey);
        }

        public override void SetNextRecipentPublicKey(byte[] key)
        {
            this.recipentNextEPK = key;
        }

        public override void HandleIncomingPacket(EncryptedPacket p)
        {
            //decrypt the GenericPacket
            //Sodium.PublicKeyBox.Open (p.CipherText, p.Nonce, this.mKeyPair.PrivateKey, this.p)

            switch (State)
            {
                case TunnelState.WaitingForHelloResponse:
                    //this should be the hello response
                    Debug.Assert(p.HasEPK, "The first GenericPacket back from a hello response has to have a public key attached to it");
                    this.recipentEPK = p.EuphemeralPublicKey;
                    this.State = TunnelState.Connected;
                    //there could be a payload attached to this GenericPacket
                    this.HandleIncomingPacket(p);

                    while (this.bufferedEncryptedPackets.Count > 0)
                    {
                        this.EncryptAndSendPacket(this.bufferedEncryptedPackets.Dequeue());
                    }
                    break;
				case TunnelState.Connected:
					Debug.Assert (recipentEPK != null);
					if (p.CipherText.Length > 0) {
						if (p.DecryptPacket (mKeyPair.PrivateKey, recipentEPK)) {
							//todo: handle out of order packets here and send acks
							PipeBase connection;
							UInt32 cid = p.CID;
							if (p.Ack > 0) {
								this.congestionController.Acked (p.Ack);
							}
							//only handle packets that have a payload or RPCs
							if (p.Payload.Length > 0 || p.RPCs.Count > 0) {
								if (cid == 0) {
									ControlPipe.HandlePacket (p);
								} else {
									if (ActivePipes.Find (ref cid, out connection)) {
										connection.HandlePacket (p);
									}
								}
							}
						}
					}
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void SendPacket(GenericPacket p)
        {
            p.destination = RemoteEndPoint;
            //TunnelSocket.SendPacket(p);
            this.congestionController.SendPacket(p);
        }

		/// <summary>
		/// Sends a single Ack out 
		/// </summary>
		/// <param name="seq">Seq.</param>
		private void SendAck(UInt32 seq){
			EncryptedPacket p = new EncryptedPacket (this.ID, 0);
			p.Ack = seq;
			//How should we handle ack packets? Should we add them to the Ack queue?
			p.Nonce = GetNextNonce();
			p.Seq = this.GetNextSeq();
			p.EncryptPacket(mKeyPair.PrivateKey, recipentEPK);
			this.congestionController.SendAck (p);
		}

        public EncryptedPacket MakeHelloPacket()
        {
            //include the public key flag
           // GenericPacket p = new GenericPacket(this.ID, 0);

            var packet = new EncryptedPacket((ID), 0, 0);
            packet.SetPayload("Hello!");
            mKeyPair = PublicKeyBox.GenerateKeyPair();
            packet.EuphemeralPublicKey = mKeyPair.PublicKey;
            packet.rawBytes = packet.PackHeader();
            return packet;
        }

        /// <summary>
        ///     Gets a new sequence number (and ensures that the number is monotonically increasing
        /// </summary>
        /// <returns>The seq number.</returns>
        protected internal UInt32 GetNextSeq()
        {
            //todo: we need to have the sequence check done before we're within range of 
            //the maximum number of packets we can send out. Things could get hairy if we have
            //to send a big GenericPacket and then rekey half way into it.
            if (mCurSeq == UInt32.MaxValue)
            {
                throw new ArgumentOutOfRangeException("Needs a rekey");
            }
            return mCurSeq++;
        }

        protected internal byte[] GetNextNonce()
        {
            //todo: do a check to see if we hit "max nonce". If that ever happens, we need a rekey.
            //the nonce should always increment by two to ensure that we dont send and recieve with the same nonce
            for (int i = 0; i < mCurNonce.Length & 0 == (2 + mCurNonce[i]); i++) ;
            return mCurNonce;
        }

        /// <summary>
        /// Handles the open GenericPacket.
        /// </summary>
        /// <param name="p">P.</param>
        public override void HandleHelloPacket(EncryptedPacket p)
        {
            State = TunnelState.HandlingHello;
            mKeyPair = PublicKeyBox.GenerateKeyPair();
            this.ID = p.TID;
            this.RemoteEndPoint = (IPEndPoint)p.sender;

            //nonces increment up by two on each side so there is no chance of the nonce being repeated.
            //on once side we have even numbers on the other side we have odds
            byte[] tmp = new byte[p.Nonce.Length];
            for (int i = 0; i < tmp.Length & 0 == ++tmp[i]; i++) ;
            mCurNonce = tmp;
            mCurSeq = p.Seq;
            recipentEPK = p.EuphemeralPublicKey;
            var responsePacket = new EncryptedPacket(ID, 0, p.Seq);
            responsePacket.EuphemeralPublicKey = mKeyPair.PublicKey;

            EncryptAndSendPacket(responsePacket);
            State = TunnelState.Connected;
        }

        /// <summary>
        ///     Closes all the existing connections and gracefully (at least it tries to be graceful)
        ///     tells the other side of the abstractTunnel its finished.
        /// </summary>
        internal override void CloseCommunications()
        {
            //todo, once we have communications established
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Sets the next TID
        /// </summary>
        internal override UInt64 NextTID()
        {
            mNextKeyPair = PublicKeyBox.GenerateKeyPair();
            mNextTID = BitConverter.ToUInt64(SodiumCore.GetRandomBytes(8), 0);
            /*if (!_socket.RegisterTunnelRekey(this, mNextTID))
            {
                NextTID();
            }*/
            //var nTID = new NextTID(mNextTID);
            //SendData(nTID.Serialize(), 0);
            return mNextTID;
        }

        internal override void SendData(byte[] data, UInt32 cid)
        {
            if (data.Length > 0)
            {
                int numPacket = (UInt16)(data.Length / recieveWindowSize);
                numPacket++;
                int offset = 0;
                for (int i = 0; i < numPacket; i++)
                {
                    int suboff = offset * i;
                    var sub = new byte[Math.Min(data.Length - suboff, recieveWindowSize)];
                    Array.Copy(data, offset, sub, 0, sub.Length);
                    var packet = new EncryptedPacket(ID, cid, 0);
                    packet.Nonce = GetNextNonce();
                    packet.Payload = sub;

                    EncryptAndSendPacket(packet);
                }
            }
            else
            {
                //send an empty GenericPacket?
            }
        }

        internal enum TunnelState
        {
            Initial,
            Starting,
            SendingHello,
            WaitingForHelloResponse,
            HandlingHello,
            Connected,
            Disconnected,
            ShuttingDown,
            Idle
        }

        #region privates

        private bool _guaranteedTransmission = false;
        private byte[] mCurNonce;
        private UInt32 mCurSeq = 1;
        public KeyPair mKeyPair;
        internal KeyPair mNextKeyPair;
        private UInt64 mNextTID;
        private int recieveWindowSize = 1546;
        public byte[] recipentEPK;
        internal byte[] recipentNextEPK;
        #endregion
    }
}
