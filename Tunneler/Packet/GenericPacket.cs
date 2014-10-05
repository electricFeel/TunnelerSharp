using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using C5;

namespace Tunneler.Packet
{
    public enum PacketType
    {
        OpenPacket,
        EncryptedPacket,
        PacketBase
    }
    /// <summary>
    /// Represents a single GenericPacket that can be sent to and from the abstractTunnel. 
    /// </summary>
    public class GenericPacket
    {
        public const UInt32 DEFAULT_ACK = 0;						//the ack when there is nothing to ack.
        public static readonly UInt16 MIN_PACKET_HEADER_SIZE = 32;	//although the overhead can be more with 
        //a GenericPacket thats sending a Euphemeral Public Key
        //and or a puzzle challange or solution
        //TID Flag Constants
        public const byte START_PAYLOAD_FLAG = 1;

        protected UInt16 packetSize;
        public EndPoint sender;
        public EndPoint destination;
        internal byte[] rawBytes;

        const uint EPK_SIZE = 32;
        const uint PUZZLE_SOLUTIONS_SIZE = 148;
        public const byte CHECKSUM_SIZE = 16;

        private byte[] _epk;				//optional (assuming the user is verified)
        private byte[] _puzzleSolution;	//optional (abstractTunnel can request it)
        
        protected bool hasEPK = false;
        protected bool hasPuzzle = false;
        protected bool HasRPC
        {
            get { return (RPCs.Count > 0); }
        }

        public ArrayList<IHasSerializationTag> RPCs { get; set; }

        /// <summary>
        /// Gets or sets the SecureAbstractTunnel ID.
        /// </summary>
        /// <value>The TID value.</value>
        public UInt64 TID
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the optional euphemeral public key.
        /// </summary>
        /// <value>The euphemeral public key.</value>
        public byte[] EuphemeralPublicKey
        {
            get { return _epk; }
            set
            {
                _epk = value;
                hasEPK = true;
            }
        }

        public UInt32 Seq
        {
            get;
            set;
        }

        public UInt32 Ack
        {
            get;
            set;
        }

        public UInt32 CID
        {
            get;
            set;
        }

        public bool HasPuzzle
        {
            get
            {
                return this.hasPuzzle;
            }
        }

        public bool HasEPK
        {
            get
            {
                return this.hasEPK;
            }
        }

        public virtual PacketType PacketType { get; protected set; }

        /// <summary>
        /// Gets or sets the optional puzzle or solution.
        /// </summary>
        /// <value>The puzzle or solution.</value>
        public byte[] PuzzleOrSolution
        {
            get { return _puzzleSolution; }
            protected set
            {
                _puzzleSolution = value;
                hasPuzzle = true;
            }
        }

        /// <summary>
        /// Gets or sets the nonce.
        /// </summary>
        /// <value>The nonce.</value>
        public byte[] Nonce
        {
            get;
            set;
        }


        protected GenericPacket()
        {
            PacketType = PacketType.PacketBase;
            this.init();
        }

        public GenericPacket(UInt16 packetSize)
        {
            this.init();
            PacketType = PacketType.PacketBase;
            this.packetSize = packetSize;
            rawBytes = new byte[packetSize];
        }

        public GenericPacket(UInt64 tid, UInt32 cid, UInt32 ack = DEFAULT_ACK)
        {
            this.init();
            this.TID = tid;
            this.Nonce = Sodium.SodiumCore.GetRandomBytes(24);
            this.CID = cid;
            this.Ack = ack;
            PacketType = PacketType.PacketBase;
        }

        private void init()
        {
            this.rawBytes = new byte[0];
            this.RPCs = new ArrayList<IHasSerializationTag>();
        }

        /// <summary>
        /// Packs the header. The total GenericPacket size is the size of the GenericPacket
        /// including the data that is going to be packed. The returning byte
        /// array will be of the size totalPacketSize + header overhead
        /// </summary>
        /// <returns>The header.</returns>
        /// <param name="totalPacketSize">Total GenericPacket size.</param>
        public byte[] PackHeader()
        {
            UInt32 packetOverhead = MIN_PACKET_HEADER_SIZE;
            if (this.hasEPK)
                packetOverhead += 32;
            if (this.hasPuzzle)
                packetOverhead += 148;
            byte[] data = new byte[packetOverhead];
            //constant start of message part
            UInt32 curIndex = 0;
            UInt64 tmpTID = this.TID;

            if (this.hasEPK)
                tmpTID |= Common.PUBLIC_KEY_FLAG;
            if (this.hasPuzzle)
                tmpTID |= Common.PUZZLE_FLAG;
            PackingHelpers.PackUint64(tmpTID, data, ref curIndex);
            //curIndex += 1;
            //pack the nonce
            Array.Copy(this.Nonce, 0, data, curIndex, 24);
            curIndex += 24;
            //TODO: Add flags to the GenericPacket header
            if (this.hasEPK)
            {
                //this.TID = this.TID |= PUBLIC_KEY_FLAG;
                Array.Copy(this.EuphemeralPublicKey, 0, data,
                    curIndex, this.EuphemeralPublicKey.Length);
                curIndex += (UInt16)this.EuphemeralPublicKey.Length;
            }

            if (this.hasPuzzle)
            {
                //this.TID = this.TID |= PUZZLE_FLAG;
                Array.Copy(this.PuzzleOrSolution, 0, data, curIndex, this.PuzzleOrSolution.Length);
                curIndex += (UInt32)this.PuzzleOrSolution.Length;
            }

            /*if (this.RPCs.Count > 0)
            {
                byte[] bytes = rpcRaw.ToArray();
                Array.Copy(bytes, 0, data, curIndex, bytes.Length);
            }*/
            return data;
        }



        /// <summary>
        /// Convienence packages that will truncate the byte array beyond a certain length
        /// </summary>
        /// <param name="length">Length of binary data we're reading</param>
        public void TruncateRaw(UInt16 length)
        {
            byte[] newRaw = new byte[length];
            Array.Copy(rawBytes, newRaw, length);
            rawBytes = newRaw;
            packetSize = (UInt16)length;
        }

        /// <summary>
        /// Attempts to create a GenericPacket from raw bytes
        /// </summary>
        /// <param name="buff">The buffer</param>
        /// <returns>A GenericPacket base</returns>
        public static GenericPacket FromBytes(byte[] buff)
        {
            GenericPacket p = new GenericPacket((UInt16)buff.Length);
            p.rawBytes = buff;
            return p;
        }

        /// <summary>
        /// Unpacks the header from the local binary representation
        /// </summary>
        public virtual uint UnpackHeader()
        {
            UInt64 tidWithFlags, tid;
            tidWithFlags = tid = 0;
            byte[] nonce = new byte[24];
            //todo: clean up the GenericPacket classes to allow us to do this without 
            UInt32 seq, ack;
            seq = ack = 0;
            byte[] epk = null, puzzle = null;
            bool hasPK, hasPuzzle;
            uint curIndex = 0;

            byte[] data = this.rawBytes;

            PackingHelpers.UnpackUint64(ref tidWithFlags, data, ref curIndex);
            //curIndex += 1;
            tid = tidWithFlags & ~Common.TID_FLAGS;
            hasPK = (tidWithFlags & Common.PUBLIC_KEY_FLAG) != 0;
            hasPuzzle = (tidWithFlags & Common.PUZZLE_FLAG) != 0;

            this.TID = tid;
            this.Seq = seq;
            this.Ack = ack;
            this.Nonce = nonce;

            //PackingHelpers.UnpackUint64 (ref nonce, data, ref curIndex);
            Array.Copy(data, curIndex, nonce, 0, nonce.Length);
            curIndex += (uint)(nonce.Length);

            if (hasPK)
            {
                epk = new byte[EPK_SIZE];
                Array.Copy(data, curIndex, epk, 0, EPK_SIZE);
                curIndex += EPK_SIZE;
                this.EuphemeralPublicKey = epk;
            }

            if (hasPuzzle)
            {
                puzzle = new byte[PUZZLE_SOLUTIONS_SIZE];
                Array.Copy(data, curIndex, puzzle, 0, PUZZLE_SOLUTIONS_SIZE);
                curIndex += PUZZLE_SOLUTIONS_SIZE;
                this.PuzzleOrSolution = puzzle;
            }

            //The remainder of the GenericPacket is encrypted. If there is an E_PK present
            //then this is likely a abstractTunnel open GenericPacket. Unlock it with the server key
            /*if(hasPK && data.Length > curIndex){
                //byte[] cipherPart = new byte[data.Length - curIndex];
                //byte[] decrypted = Sodium.PublicKeyBox.OpenDetached (cipherPart, nonce, epk, secretKey);
                //copy in the checksum
                //byte[] rawMessage = new byte[decrypted.Length - CHECKSUM_SIZE];
                //Array.Copy (decrypted, rawMessage, 0, rawMessage.Length);
                //here we have to create an OpenPacket
                byte[] cipherPart = new byte[data.Length - curIndex];
                Array.Copy (data, curIndex, cipherPart, 0, cipherPart.Length);
                if (!hasPuzzle) {
                    OpenTunnelPacket GenericPacket = new OpenTunnelPacket (tid, seq, id, ack, epk, cipherPart);
                    GenericPacket.Nonce = nonce;

                }
            }else{*/
            //EncryptedPacket GenericPacket = new EncryptedPacket(tid, id, ack);
            //GenericPacket.Seq = seq;
            //GenericPacket.Nonce = nonce;

            //GenericPacket.CipherText = cipherPart;
            return curIndex;
            //}
        }

        /// <summary>
        /// Unpacks the header.
        /// </summary>
        /// <returns>The header.</returns>
        /// <param name="data">Data.</param>
        /// <param name="serverKey">Server key.</param>
        /// <param name="refOffset">Reference offset.</param>
        public static GenericPacket UnpackPacket(byte[] data)
        {
            UInt64 tidWithFlags, tid;
            tidWithFlags = tid = 0;
            byte[] nonce = new byte[24];
            //todo: clean up the GenericPacket classes to allow us to do this without 
            UInt32 seq, ack, cid;
            seq = ack = cid = 0;
            byte[] epk = null, puzzle = null;
            bool hasPK, hasPuzzle;
            uint curIndex = 0;

            PackingHelpers.UnpackUint64(ref tidWithFlags, data, ref curIndex);
            //curIndex += 1;
            tid = tidWithFlags & ~Common.TID_FLAGS;
            hasPK = (tidWithFlags & Common.PUBLIC_KEY_FLAG) == Common.PUBLIC_KEY_FLAG;
            hasPuzzle = (tidWithFlags & Common.PUZZLE_FLAG) == Common.PUZZLE_FLAG;

            //PackingHelpers.UnpackUint64 (ref nonce, data, ref curIndex);
            Array.Copy(data, curIndex, nonce, 0, nonce.Length);
            curIndex += (uint)(nonce.Length);

            if (hasPK)
            {
                epk = new byte[EPK_SIZE];
                Array.Copy(data, curIndex, epk, 0, EPK_SIZE);
                curIndex += (EPK_SIZE + 1);
            }

            if (hasPuzzle)
            {
                puzzle = new byte[PUZZLE_SOLUTIONS_SIZE];
                Array.Copy(data, curIndex, puzzle, 0, PUZZLE_SOLUTIONS_SIZE);
                curIndex += PUZZLE_SOLUTIONS_SIZE + 1;
            }

            //The remainder of the GenericPacket is encrypted. If there is an E_PK present
            //then this is likely a abstractTunnel open GenericPacket. Unlock it with the server key
            if (hasPK && data.Length > curIndex)
            {
                //byte[] cipherPart = new byte[data.Length - curIndex];
                //byte[] decrypted = Sodium.PublicKeyBox.OpenDetached (cipherPart, nonce, epk, secretKey);
                //copy in the checksum
                //byte[] rawMessage = new byte[decrypted.Length - CHECKSUM_SIZE];
                //Array.Copy (decrypted, rawMessage, 0, rawMessage.Length);
                //here we have to create an OpenPacket
                //todo: is this strictly nessecary?
                byte[] cipherPart = new byte[data.Length - curIndex];
                Array.Copy(data, curIndex, cipherPart, 0, cipherPart.Length);
                if (!hasPuzzle)
                {
                    OpenTunnelPacket packet = new OpenTunnelPacket(tid, seq, cid, ack, epk, cipherPart);
                    packet.Nonce = nonce;
                    return packet;
                }
                return new OpenTunnelPacket(tid, seq, cid, ack, epk, cipherPart, puzzle);
            }
            else
            {
                EncryptedPacket packet = new EncryptedPacket(tid, cid, ack);
                packet.Seq = seq;
                packet.Nonce = nonce;
                byte[] cipherPart = new byte[data.Length - curIndex];
                Array.Copy(data, curIndex, cipherPart, 0, cipherPart.Length);
                packet.CipherText = cipherPart;
                return packet;
            }

       
        }

        /// <summary>
        /// Generates the raw bytes
        /// </summary>
        /// <returns></returns>
        public virtual byte[] ToBytes()
        {
            return rawBytes;
        }
    }



    /// <summary>
    /// The open GenericPacket is an encrypted GenericPacket with and additional property
    /// that maintains the SecretBox. this is a convienence for the sake of
    /// 
    /// </summary>
    public class OpenTunnelPacket : EncryptedPacket
    {
        public override PacketType PacketType
        {
            get
            {
                return PacketType.OpenPacket;
            }
        }

        public OpenTunnelPacket(UInt64 tid, UInt32 seq, UInt32 cid, UInt32 ack,
            byte[] pk, byte[] ciphered)
            : base(tid, cid, ack)
        {
            this.Seq = seq;
            this.EuphemeralPublicKey = pk;
        }

        public OpenTunnelPacket(UInt64 tid, UInt32 seq, UInt32 cid, UInt32 ack,
            byte[] pk, byte[] ciphered, byte[] puzzle)
            : base(tid, cid, ack)
        {
            this.Seq = seq;
            this.EuphemeralPublicKey = pk;
            this.PuzzleOrSolution = puzzle;
        }
    }
}
