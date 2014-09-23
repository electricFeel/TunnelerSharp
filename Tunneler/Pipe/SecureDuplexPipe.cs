using System;
using Tunneler.Pipe;
using Sodium;
using MsgPack.Serialization;
using System.Security.Cryptography;

namespace Tunneler
{
	/// <summary>
	/// A SecureDuplexPipe is a pipe that provides it's own end-to-end encryption
	/// within the already encrypted tunnel. This allows each instance of the pipe
	/// to have added encryption and privacy. 
	/// </summary>
	public class SecureDuplexPipe : DuplexPipe
	{
		internal enum SecureDuplexState{
			Initializing,
			AwaitingKeyResponse,
			PipeSecure
		}
		internal SecureDuplexState _secureState = SecureDuplexState.Initializing;
		internal KeyPair keyPair;
		internal byte[] recipentEPK;
		//internal MessagePackSerializer
		public override PipeType Type {
			get {
				return PipeType.SecureDuplex;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Tunneler.SecureDuplexPipe"/> class.
		/// A SecureDuplexPipe is a pipe with it's own Public Key encryption setup from 
		/// end-to-end. This enables another layer of protection over the encryption
		/// provided by the tunnel itself
		/// </summary>
		public SecureDuplexPipe (TunnelBase tunnel):base(tunnel)
		{

		}

		public SecureDuplexPipe(TunnelBase tunnel, UInt32 pipeId):base(tunnel, pipeId)
		{

		}

		internal override void ChangeState (PipeState newState)
		{
			if(_state == PipeState.AwaitingAck && newState == PipeState.Connected){
				//we need to do a key exchange before we can be ready to send anything.
				if(keyPair == null){
					keyPair = Sodium.PublicKeyBox.GenerateKeyPair ();
					SecureMessage message = new SecureMessage ();
					message.PublicKey = keyPair.PublicKey;
					base.Send (MessagePackSerializer.Get<SecureMessage> ().PackSingleObject (message));
					this._secureState = SecureDuplexState.AwaitingKeyResponse;
				}
			}else{
				base.ChangeState (newState);
			}
		}

		internal override void OnMessageAssembled(byte[] message){
			switch(_secureState)
			{
				case SecureDuplexState.Initializing:
					//should be a key
					this.recipentEPK = MessagePackSerializer.Get<SecureMessage> ().UnpackSingleObject (message).PublicKey;
					break;
				case SecureDuplexState.AwaitingKeyResponse:
					this.recipentEPK = MessagePackSerializer.Get<SecureMessage> ().UnpackSingleObject (message).PublicKey;
					break;
				case SecureDuplexState.PipeSecure:
					//should be data (but can be a key)
					SecureMessage smsg = MessagePackSerializer.Get<SecureMessage> ().UnpackSingleObject (message);
					this.HandleSecureMessage (smsg);
					break;
			}
		}

		private void HandleSecureMessage(SecureMessage msg){
			if(msg.PublicKey.Length == 32){
				this.recipentEPK = msg.PublicKey;
			}

			if(msg.Payload.Length > 0){
				base.OnMessageAssembled (msg.Payload);
			}
		}

		public override void HandlePacket (Tunneler.Packet.EncryptedPacket packet)
		{
			base.HandlePacket (packet);
		}

		 

		public class SecureMessage
		{
			public byte[] PublicKey { get; set;}
			public byte[] Payload { get; set;}

			internal SecureMessage()
			{
				this.PublicKey = new byte[0];
				this.Payload = new byte[0];
			}
		}
	}
}

