using System;
using Tunneler.Packet;

namespace Tunneler.Pipe
{
    /// <summary>
    /// I'm not quite sure what this looks like yet but its coming...
    /// </summary>
    public enum PipeType
    {
        Control,
        Duplex,
		SecureDuplex
    }

	/// <summary>
	/// Maintains the global state for the pipe.
	/// </summary>
	public enum PipeState
	{
		AwaitingAck,
		Connected,
		Disconnected
	}

    /// <summary>
    /// A basic connection. A connection is created by a tunnel and has access
    /// to the tunnel for the purpose of sending and recieving data.
    /// </summary>
    public abstract class PipeBase
    {
		internal PipeState _state = PipeState.AwaitingAck;
        protected TunnelBase mTunnel;
        public UInt32 ID { get; protected set; }
		public abstract PipeType Type { get;}

        public PipeBase(TunnelBase tunnel)
        {
            mTunnel = tunnel;
            BitConverter.ToUInt32(Sodium.SodiumCore.GetRandomBytes(32), 0);
        }

        public PipeBase(TunnelBase tunnel, UInt32 cid)
        {
            this.ID = cid;
            this.mTunnel = tunnel;
        }

        public PipeBase(UInt32 cid)
        {
            this.ID = cid;
        }

        public abstract void HandlePacket(EncryptedPacket packet);
		internal virtual void ChangeState(PipeState newState){
			_state = newState;
		}
    }
}
