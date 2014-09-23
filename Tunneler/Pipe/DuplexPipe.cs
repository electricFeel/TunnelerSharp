using System;
using Tunneler.Packet;

namespace Tunneler.Pipe
{
    public delegate void DataReceivedHandler(object sender, DataReceivedEventArgs args);
    /// <summary>
    /// A two way communication connection.
    /// </summary>
    public class DuplexPipe : PipeBase
    {
        protected const UInt16 MAX_MESSAGE_SIZE = UInt16.MaxValue;
        protected enum PacketHandleState
        {
            ParsingMessage,
            Idle
        }

		public override PipeType Type {
			get {
				return PipeType.Duplex;
			}
		}

        private PacketHandleState mPacketHandleState = PacketHandleState.Idle;
        private byte[] parseBuffer;
        private int parseIndex = -1;
        /*
         * Use the ZMQ message frame length scheme: http://api.zeromq.org/2-1:zmq-tcp
         */
        public event DataReceivedHandler DataReceived;
        public DuplexPipe(TunnelBase tunnel)
            : base(tunnel)
        {

        }

        public DuplexPipe(TunnelBase tunnel, UInt32 cid)
            : base(tunnel, cid)
        {

        }

        public void Send(String message)
        {
            this.Send(System.Text.Encoding.ASCII.GetBytes(message));
        }

        public void Send(byte[] message)
        {
            if (message.Length > MAX_MESSAGE_SIZE)
                throw new ArgumentException("Message is too large");
            //messages need to be framed....
            this.FrameAndSendMessage(message);
        }

        public override void HandlePacket(EncryptedPacket packet)
        {
            switch (this.mPacketHandleState)
            {
                case PacketHandleState.Idle:
                    this.mPacketHandleState = PacketHandleState.ParsingMessage;
                    HandleIncomingMessage(packet);
                    break;
                case PacketHandleState.ParsingMessage:
                    HandleIncomingMessage(packet);
                    break;
            }
        }

        protected void HandleIncomingMessage(EncryptedPacket packet)
        {
            int startIndex = 0;
            if (parseBuffer == null)
            {
                //this is the first packet read the first two bytes
                //to get the message length
                this.parseBuffer = new byte[BitConverter.ToUInt16(packet.Payload, 0)];
                startIndex = 2;
                this.parseIndex = 0;
            }
            int payloadLen = (packet.Payload.Length - startIndex);
            Array.Copy(packet.Payload, startIndex, parseBuffer, parseIndex, payloadLen);
            parseIndex += payloadLen;
            if (parseIndex >= parseBuffer.Length)
            {
                //raise message recieved
				OnMessageAssembled (parseBuffer);
                parseBuffer = null;
                parseIndex = -1;
            }
        }

		internal virtual void OnMessageAssembled(byte[] message){
			byte[] data = new byte[message.Length];
			Array.Copy(message, data, message.Length);
			if (DataReceived != null)
				DataReceived(this, new DataReceivedEventArgs(data));
		}

        protected void FrameAndSendMessage(byte[] message)
        {
            UInt16 size = (UInt16)Math.Min(mTunnel.GetMaxPayloadSize(), message.Length);
            byte[] sizeByte = BitConverter.GetBytes((UInt16)message.Length);
			uint numPacket = (uint)Math.Ceiling((double)((message.Length + 2.0) / size));
            int curPacket = 0;
            int sectionIndex = 0;
            byte[] payload = new byte[size];
            //the frame is prefixed by the size.
            Array.Copy(sizeByte, payload, sizeByte.Length);
            sectionIndex += sizeByte.Length;
            int messageIndex = 0;
            while (curPacket < numPacket)
            {
                int sectionLen = message.Length - (messageIndex);
                if (curPacket != 0 && sectionLen < size)
                {
                    payload = new byte[sectionLen];
                }
                else
                {
					sectionLen = size - sectionIndex;
                }
                Array.Copy(message, messageIndex, payload, sectionIndex, (sectionLen));
                this.mTunnel.SendData(payload, this.ID);
                messageIndex += (sectionLen);
                curPacket++;
                sectionIndex = 0;
            }
        }
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; private set; }
        public DataReceivedEventArgs(byte[] data)
            : base()
        {
            this.Data = data;
        }
    }
}
