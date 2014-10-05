using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using C5;
using Sodium;


namespace Tunneler.Packet
{
    /// <summary>
    /// An encrypted packet is the most common type of packet. It will take in a raw
    /// byte array, encrypt it along with the header. To bytes will return the byte array
    /// so it can be pushed down to the socket 
    /// </summary>
    public class EncryptedPacket : GenericPacket
    {
        public override PacketType PacketType
        {
            get
            {
                return PacketType.EncryptedPacket;
            }
        }

        /// <summary>
        /// Payload to be sent down the wire
        /// </summary>
        /// <value>The payload.</value>
        public byte[] Payload
        {
            get;
            set;
        }

        public byte[] CipherText
        {
            get;
            set;
        }

        public EncryptedPacket(UInt16 mtuSize)
        {
            this.rawBytes = new byte[mtuSize];
            this.init();
        }

        public EncryptedPacket(UInt64 tid, UInt32 cid)
            : base(tid, cid)
        {
            this.init();
        }

        public EncryptedPacket(UInt64 tid, UInt32 cid, UInt32 ack)
            : base(tid, cid, ack)
        {
            this.init();
        }

        public EncryptedPacket(UInt64 tid, UInt32 seq, UInt32 cid, UInt32 ack,
            IPEndPoint sendTo, byte[] payload)
            : base(tid, cid, ack)
        {
            this.Payload = payload;
            this.destination = sendTo;
            this.init();
        }

        public void SetPayload(String text)
        {
            byte[] bytes = new byte[text.Length * sizeof(char)];
            System.Buffer.BlockCopy(text.ToCharArray(), 0, bytes, 0, bytes.Length);
            this.Payload = bytes;
        }

        private void init()
        {
            this.Payload = new byte[0];
            this.CipherText = new byte[0];
        }

        public override uint UnpackHeader()
        {
            byte[] data = rawBytes;
            uint curIndex = base.UnpackHeader();
            byte[] cipherPart = new byte[data.Length - curIndex];
            Array.Copy(data, curIndex, cipherPart, 0, cipherPart.Length);
            this.CipherText = cipherPart;
            return curIndex + (uint)this.CipherText.Length;
        }

        /// <summary>
        /// Encrypts and packs the packet for sending down the wire.
        /// </summary>
        /// <returns>The packet.</returns>
        /// <param name="sk">Local (sender) secret key.</param>
        /// <param name="pk">Remote (recipent) public key</param>
        public byte[] EncryptPacket(byte[] sk, byte[] pk)
        {
            byte[] header = this.PackHeader();
            //seq + ack + id + payload
            MemoryStream rpcRaw = new MemoryStream();
            if (this.HasRPC)
            {
                if (this.RPCs.Count > 0)
                {
                    rpcRaw.WriteByte((byte)RPCType.RPCStart);
                    foreach (IHasSerializationTag rpc in this.RPCs)
                    {
                        byte[] curRpcBytes = rpc.Serialize();
                        rpcRaw.WriteByte(rpc.SerializationTag);
                        rpcRaw.Write(curRpcBytes, 0, curRpcBytes.Length);
                    }
                    rpcRaw.WriteByte((byte)RPCType.RPCEnd);
                }
            }
            rpcRaw.Close();

            byte[] rpcBytes = rpcRaw.ToArray();
            int totalLength = 4 + 4 + 4;
            if (rpcBytes.Length > 0)
            {
                totalLength += rpcBytes.Length;
            }
            totalLength++;
            totalLength += this.Payload.Length;
            byte[] encryptedPart = new byte[totalLength];
            uint index = 0;
            PackingHelpers.PackUint32(Seq, encryptedPart, ref index);
            PackingHelpers.PackUint32(Ack, encryptedPart, ref index);
            PackingHelpers.PackUint32(CID, encryptedPart, ref index);
            if (rpcBytes.Length > 0)
            {
                Array.Copy(rpcBytes, 0, encryptedPart, index, rpcBytes.Length);
                index += (uint)rpcBytes.Length;
            }
            //start message flag.
            encryptedPart[index] = START_PAYLOAD_FLAG;
            ++index;
            Array.Copy(this.Payload, 0, encryptedPart, index, this.Payload.Length);
            byte[] ciphered = PublicKeyBox.Create(encryptedPart, this.Nonce, sk, pk);
            this.CipherText = ciphered;
            byte[] ret = new byte[header.Length + ciphered.Length];
            Array.Copy(header, ret, header.Length);
            Array.Copy(ciphered, 0, ret, header.Length, ciphered.Length);
            this.rawBytes = ret;
            return ret;
        }

        public bool DecryptPacket(byte[] sk, byte[] pk)
        {
            byte[] decrypt;
            try
            {
                decrypt = Sodium.PublicKeyBox.Open(this.CipherText, this.Nonce, sk, pk);
            }
            catch (CryptographicException ex)
            {
                Debug.Fail(ex.StackTrace);
                return false;
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.StackTrace);
                return false;
            }
            MemoryStream ms = new MemoryStream(decrypt);

            UInt32 seq = 0;
            UInt32 ack = 0;
            UInt32 cid = 0;
            PackingHelpers.UnpackUint32(ref seq, ms);
            PackingHelpers.UnpackUint32(ref ack, ms);
            PackingHelpers.UnpackUint32(ref cid, ms);
            this.Seq = seq;
            this.Ack = ack;
            this.CID = cid;
            int flag = ms.ReadByte();
            Debug.Assert((flag == (int)RPCType.RPCStart || flag == (int)START_PAYLOAD_FLAG));
            if (flag == (int)RPCType.RPCStart)
            {

                //deserialize the RPC
                this.RPCs = new ArrayList<IHasSerializationTag>();
                byte serializationTag;
                while ((serializationTag = (byte)ms.ReadByte()) != (byte)RPCType.RPCEnd)
                {
                    IHasSerializationTag rpc = null;
                    switch ((RPCType)serializationTag)
                    {
                        case RPCType.AnonymousPipe:
                            rpc = RPCBase<CreateAnonymousPipe>.Unpack(ms);
                            break;
                        case RPCType.AuthenticatedPipe:
                            rpc = RPCBase<CreateAuthenticatedPipe>.Unpack(ms);
                            break;
                        case RPCType.ClosePipe:
                            rpc = RPCBase<ClosePipeRPC>.Unpack(ms);
                            break;
                        case RPCType.AckPipe:
                            rpc = RPCBase<AckPipe>.Unpack(ms);
                            break;
                        case RPCType.RefusePipe:
                            rpc = RPCBase<RefusePipe>.Unpack(ms);
                            break;
                        case RPCType.RequestCertificate:
                            rpc = RPCBase<RequestCertificate>.Unpack(ms);
                            break;
                        case RPCType.GiveCertificate:
                            rpc = RPCBase<GiveCertificate>.Unpack(ms);
                            break;
                        case RPCType.Ok:
                            rpc = RPCBase<OkRPC>.Unpack(ms);
                            break;
                        case RPCType.NextTID:
                            rpc = RPCBase<NextTID>.Unpack(ms);
                            break;
                        case RPCType.RekeyNow:
                            rpc = RPCBase<RekeyNow>.Unpack(ms);
                            break;
                        case RPCType.PosePuzzle:
                            rpc = RPCBase<PosePuzzle>.Unpack(ms);
                            break;
                        case RPCType.PuzzleSolution:
                            //RPCBase<>.Unpack(ms);
                            //major todo
                            break;
                        case RPCType.WindowResize:
                            rpc = RPCBase<ResizeWindow>.Unpack(ms);
                            break;
                        default:
                            //todo we should error out but simply drop packets
                            throw new ArgumentOutOfRangeException();
                    }
                    this.RPCs.Add(rpc);
                }
                flag = ms.ReadByte();
            }

            if (flag == START_PAYLOAD_FLAG)
            {
                int index = (int)ms.Position;
                byte[] payload = new byte[decrypt.Length - index];
                Array.Copy(decrypt, index, payload, 0, payload.Length);
                this.Payload = payload;
            }
            else
            {
                this.Payload = new byte[0];
            }
            return true;
        }
    }
}
