using System;
using System.Diagnostics;
using C5;
using Sodium;
using Tunneler.Packet;
using System.Threading.Tasks;

namespace Tunneler.Pipe
{
    /// <summary>
    ///     A control is really a "tunnel management" pipes.
    /// </summary>
    public class ControlPipe : PipeBase, IDisposable
    {
        private bool rekeyReady = false;
		public override PipeType Type {
			get {
				return PipeType.Control;
			}
		}

        /// <summary>
        /// Initializes a new instance of the <see cref="Tunnel.ControlPipe"/> class 
        /// locally with the specified port. 
        /// </summary>
        /// <param name="port">Port.</param>
        public ControlPipe(short port)
            : base(0)
        {
            mTunnel = new SecureTunnel(TunnelRuntime.GetOrCreateTunnelSocket(port));
        }

        public ControlPipe(TunnelBase tunnel)
            : base(tunnel, 0)
        {
        }

        public void Dispose()
        {
            CloseTunnel();
        }

		public event PipeChangedEventHandler NewPipe;
		public event PipeChangedEventHandler PipeClosed;
		public event PipeChangedEventHandler PipeRefused;
		public event RPCEventHandler RPCRefused;

        public PipeBase OpenNewPipe(PipeType type, uint id)
        {
            return this.OpenPipe(type, id);
        }

        public void CloseExistingPipe(uint id)
        {
            this.ClosePipe(id, false);
        }

        public override void HandlePacket(EncryptedPacket packet)
        {
            foreach (IHasSerializationTag rpc in packet.RPCs)
            {
                switch ((RPCType)rpc.SerializationTag)
                {
                    case RPCType.AnonymousPipe:
                        HandleOpenAnonymousPipeRequest(rpc);
                        break;
                    case RPCType.AuthenticatedPipe:
                        HandleOpenAuthenticatedPipeRequest(rpc);
                        break;
                    case RPCType.ClosePipe:
                        HandleClosePipeRequest(rpc);
                        break;
                    case RPCType.AckPipe:
                        HandleAckPipeRequest(rpc);
                        break;
                    case RPCType.RefusePipe:
                        HandleRefusePipeRequest(rpc);
                        break;
                    case RPCType.RequestCertificate:
                        HandleRequestCertificateRequest(rpc);
                        break;
                    case RPCType.GiveCertificate:
                        HandleRecieveCertificate(rpc);
                        break;
                    case RPCType.Ok:
                        HandleOk(rpc);
                        break;
                    case RPCType.Refuse:
                        HandleRefuse(rpc);
                        break;
                    case RPCType.NextTID:
                        HandleNextTID(rpc);
                        break;
                    case RPCType.RekeyNow:
                        HandleReKeyNow(rpc);
                        break;
                    case RPCType.PosePuzzle:
                        HandlePosePuzzle(rpc);
                        break;
                    case RPCType.PuzzleSolution:
                        HandleSolvePuzzle(rpc);
                        break;
                    case RPCType.WindowResize:
                        HandleWindowResize(rpc);
                        break;
                    case RPCType.PrepareRekey:
                        HandlePrepareRekey(rpc);
                        break;
                    default:
                        Debug.Fail("Handling an unknown RPC type");
                        break;
                }
            }
        }

        private void HandleRefuse(IHasSerializationTag rpc)
        {
            Refuse refuse = (Refuse)rpc;
            //todo
        }

        private void HandlePrepareRekey(IHasSerializationTag rpc)
        {
            var p = (PrepareRekey)rpc;
            this.mTunnel.SetNextRecipentPublicKey(p.NextPublicKey);
            RekeyResponse response = new RekeyResponse((PrepareRekey)this.mTunnel.PrepareRekey());
            EncryptedPacket packet = new EncryptedPacket(this.mTunnel.ID, this.ID);
            packet.RPCs.Add(response);
            this.mTunnel.EncryptAndSendPacket(packet);
            rekeyReady = true;
        }

        private void RefusePipe(UInt32 id, RefusePipe.RefusalReason reason)
        {
            var p = new EncryptedPacket(mTunnel.ID, id);
            p.RPCs.Add(new RefusePipe(id, reason));
            mTunnel.EncryptAndSendPacket(p);
        }

        private void SendOk(UInt32 rpid)
        {
            OkRPC ok = new OkRPC(rpid);
            EncryptedPacket p = new EncryptedPacket(this.mTunnel.ID, this.ID);
            p.RPCs.Add(ok);
            this.mTunnel.EncryptAndSendPacket(p);
        }

        private void SendRefuse(UInt32 rpid)
        {
            Refuse refuse = new Refuse(rpid);
            EncryptedPacket p = new EncryptedPacket(this.mTunnel.ID, this.ID);
            p.RPCs.Add(refuse);
            this.mTunnel.EncryptAndSendPacket(p);
        }

        public void CloseTunnel()
        {
            //close existing pipse
            foreach (uint pipeID in this.mTunnel.PipeIDs)
            {
                if (pipeID != 0)
                {
                    this.ClosePipe(pipeID);
                }

            }
            //close any open pipe requests
            foreach (KeyValuePair<uint, PipeBase> requestedPipe in requestedPipes)
            {
                this.ClosePipe(requestedPipe.Key);
            }

            //todo: unregister the tunnel from the tunnel directory.
        }

        #region Request Handling

        private void HandleClosePipeRequest(IHasSerializationTag rpc)
        {
            var c = (ClosePipeRPC)rpc;
            mTunnel.ClosePipe(c.ID);
            SendOk(c.RequestID);
        }

        private void HandleOpenAuthenticatedPipeRequest(IHasSerializationTag rpc)
        {
            //todo: figure out how to "authenticate" a pipe
        }

        private void HandleOpenAnonymousPipeRequest(IHasSerializationTag rpc)
        {
            var c = (CreateAnonymousPipe)rpc;
            //handle the pipe type....
            PipeType ctype;
            if (Enum.TryParse(c.PipeType, out ctype))
            {
                //todo: how do we want to handle anonymous pipes? should we make a policy mechanisim?
                if (mTunnel.PipeIDExists(c.ID))
                {
                    RefusePipe(c.ID, Packet.RefusePipe.RefusalReason.ID_ALREADY_EXISTS);
                }
                else if (c.ID == 0)
                {
                    RefusePipe(c.ID, Packet.RefusePipe.RefusalReason.CANNOT_OPEN_ANOTHER_CONTROL);
                }
                else
                {
                    //create the pipe
                    PipeBase createdPipe = null;
                    switch (ctype)
                    {
                        case PipeType.Control:
                            RefusePipe(c.ID, Packet.RefusePipe.RefusalReason.UNSUPPORTED_PIPE_TYPE);
                            break;
                        case PipeType.Duplex:
                            createdPipe = new DuplexPipe(mTunnel, c.ID);
                            mTunnel.OpenPipe(createdPipe);
                            OnPipeCreated(createdPipe);
                            break;
                        default:
                            RefusePipe(c.ID, Packet.RefusePipe.RefusalReason.UNSUPPORTED_PIPE_TYPE);
                            Debug.Fail("Unknown Pipe Type");
                            break;
                    }
                    if (createdPipe != null)
                    {
                        var packet = new EncryptedPacket(mTunnel.ID, ID);
                        packet.RPCs.Add(new AckPipe(createdPipe.ID));
                        mTunnel.EncryptAndSendPacket(packet);
                    }
                }
            }
            else
            {
                RefusePipe(c.ID, Tunneler.Packet.RefusePipe.RefusalReason.UNKNOWN);
            }
        }

        private void HandleRefusePipeRequest(IHasSerializationTag rpc)
        {
            var c = (RefusePipe)rpc;
            OnPipeRefused(c);
        }

        private void HandleRequestCertificateRequest(IHasSerializationTag rpc)
        {
            throw new NotImplementedException("We need to figure out how to setup a certificate");
        }

        private void HandleAckPipeRequest(IHasSerializationTag rpc)
        {
            //pipe request has been accepted by the otherside
            var ack = (AckPipe)rpc;
            UInt32 id = ack.ID;
            PipeBase pipe;
            if (this.requestedPipes.Remove(id, out pipe))
            {
				//register the pipe to the tunnel and 
				//change the pipe state to connected
                this.mTunnel.OpenPipe(pipe);
                this.OnPipeCreated(pipe);
                //send an ok?
            }
            else
            {
                //todo: handle error
            }
        }

        private void HandleRecieveCertificate(IHasSerializationTag rpc)
        {
            //todo
        }

        private void HandleOk(IHasSerializationTag rpc)
        {
            //todo -- this has to be related to the RPC requests
        }

        private void HandleNextTID(IHasSerializationTag rpc)
        {
            NextTID nextTid = (NextTID)rpc;
            this.mTunnel.NextTID(nextTid.TID);
            this.SendOk(rpc.RequestID);
        }

        private void HandleReKeyNow(IHasSerializationTag rpc)
        {
            if (rekeyReady)
            {
                this.mTunnel.RekeyNow();
                this.SendOk(rpc.RequestID);
                this.rekeyReady = false;
            }
            else
            {
                this.SendRefuse(rpc.RequestID);
            }
        }

        private void HandlePosePuzzle(IHasSerializationTag rpc)
        {
        }

        private void HandleSolvePuzzle(IHasSerializationTag rpc)
        {
        }

        private void HandleWindowResize(IHasSerializationTag rpc)
        {
            //used for flow control...major todo
        }

        /// <summary>
        ///     Opens a pipe request with the other side of the tunnel.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        private PipeBase OpenPipe(PipeType type, UInt32 id = 0)
        {
            //the connecting party doesn't care about the pipe ID.
            if (id == 0)
            {
                id = BitConverter.ToUInt32(SodiumCore.GetRandomBytes(4), 0);
            }
            IHasSerializationTag c = new CreateAnonymousPipe(type.ToString(), id);
            PipeBase pipe = null;
            switch (type)
            {
                case PipeType.Control:
                    throw new NotSupportedException(
                        "Cannot create a new control pipe where on a tunnel that already has a control pipe");
                case PipeType.Duplex:
                    pipe = new DuplexPipe(this.mTunnel, id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
            this.requestedPipes.Add(id, pipe);
            EncryptedPacket packet = new EncryptedPacket(this.mTunnel.ID, this.ID);
            packet.RPCs.Add(c);
            this.mTunnel.EncryptAndSendPacket(packet);
            return pipe;
        }

        /// <summary>
        /// Sends a close request
        /// </summary>
        /// <param name="id">pipe id</param>
        /// <param name="waitForAck">Flag the waits until the close is acknowledged before removing the object instance</param>
        public void ClosePipe(UInt32 id, bool waitForAck = false)
        {
            EncryptedPacket p = new EncryptedPacket(this.mTunnel.ID, this.ID);
            p.RPCs.Add(new ClosePipeRPC(id));
            this.mTunnel.EncryptAndSendPacket(p);

            if (!waitForAck)
            {
                this.mTunnel.ClosePipe(id);
            }
            else
            {
                this.requestedClosedPipes.Add(id);
            }
        }

        #endregion

        #region NewPipe Stubs

        //These are the events raised whenever a paticular request is made on the control pipe
        private void OnPipeCreated(PipeBase pipe)
        {
			if (this.NewPipe != null)
				this.NewPipe (this, new PipeChangedEventArgs (pipe, this.mTunnel));
        }

		private void OnPipeClosed(PipeBase pipe){
			if (this.PipeClosed != null)
				this.PipeClosed (this, new PipeChangedEventArgs (pipe, this.mTunnel));
		}


        private void OnPipeRefused(RefusePipe rpc)
        {
			PipeBase p;
			UInt32 id = rpc.ID;
			this.requestedPipes.Remove (id, out p);
			Debug.Assert (p != null);
			if(this.PipeRefused != null){
				this.PipeRefused (this, new PipeChangedEventArgs (p, this.mTunnel));
			}
		}

        private void OnRPCRefused(Refuse rpc)
        {
			if (this.RPCRefused != null)
				this.RPCRefused (this, new RPCEventArgs (rpc, this.mTunnel));
        }

        #endregion

        #region private variables

        //requestedPipes maintians the PipeBase objects that have been requested but not yet created.
        private C5.HashDictionary<UInt32, PipeBase> requestedPipes = new HashDictionary<UInt32, PipeBase>();
        private ArrayList<UInt32> requestedClosedPipes = new ArrayList<UInt32>();
        #endregion
    }

	#region Event Helpers
	public class PipeChangedEventArgs:EventArgs
	{
		public PipeBase Pipe { get; private set; }
		public TunnelBase Tunnel { get; private set;}
		public PipeChangedEventArgs(PipeBase pipe, TunnelBase tunnel){
			this.Pipe = pipe;
			this.Tunnel = tunnel;
		}
	}

	public class RPCEventArgs:EventArgs
	{
		public IHasSerializationTag RPC { get; private set;}
		public TunnelBase Tunnel { get; private set;}
		public RPCEventArgs(IHasSerializationTag rpc, TunnelBase tunnel){
			this.RPC = rpc;
			this.Tunnel = tunnel;
		}
	}

	public delegate void RPCEventHandler(object sender, RPCEventArgs args);
	public delegate void PipeChangedEventHandler(object sender, PipeChangedEventArgs args);
	#endregion
}
