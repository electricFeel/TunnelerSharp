using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MsgPack.Serialization;
using Sodium;

namespace Tunneler.Packet
{
    //MAJOR TODO: MAKE ALL OF THESE CLASSES INTERNAL AND MARK THEM AS FRIENDS OF BOTH MESSAGEPACK 
    //AND THE TUNNELTEST.
    public interface IHasSerializationTag
    {
        byte SerializationTag { get; }
        byte[] Serialize();
        /// <summary>
        /// A request ID allows for a response/request to by uniquely identified
        /// </summary>
        UInt32 RequestID { get; }
    }
    /*public interface IRemoteProcCommand<T>{

        byte[] Serialize();
        T Deserialize(MemoryStream ms);
        T Deserialize(byte[] buffer);
    }*/

    public abstract class RPCBase<T> : IHasSerializationTag
    {
        protected UInt32 _id = 0;
        public abstract byte SerializationTag { get; }
        public abstract T Deserialize(MemoryStream ms);
        public abstract T Deserialize(byte[] buffer);
        public abstract byte[] Serialize();

        public uint RequestID
        {
            get
            {
                if (_id == 0)
                {
                    _id = BitConverter.ToUInt32(SodiumCore.GetRandomBytes(4), 0);
                }
                return _id;
            }
        }

        public static T Unpack(byte[] buffer)
        {
            return MessagePackSerializer.Get<T>().UnpackSingleObject(buffer);
        }

        public static T Unpack(MemoryStream buffer)
        {
            return MessagePackSerializer.Get<T>().Unpack(buffer);
        }
    }
    /// <summary>
    /// RPC have a type tag used in serialization and deserialization
    /// </summary>
    public enum RPCType : byte
    {
        RPCEnd = 255,
        RPCStart = 0,
        NoRpc = 1,
        AnonymousPipe = 2,
        AuthenticatedPipe = 3,
        ClosePipe = 4,
        AckPipe = 5,
        RefusePipe = 6,
        RequestCertificate = 7,
        GiveCertificate = 8,
        Ok = 9,
        Refuse = 10,
        NextTID = 11,
        RekeyNow = 12,
        PosePuzzle = 13,
        PuzzleSolution = 14,
        WindowResize = 15,
        PrepareRekey = 16,
        RekeyResponse = 17,
    }

    /// <summary>
    /// An anonymous pipe is, more or less, a "Hello" GenericPacket. Once a tunnel
    /// is established, the connector may no longer be considered anonymous
    /// </summary>
    public class CreateAnonymousPipe : RPCBase<CreateAnonymousPipe>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.AnonymousPipe;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }


        public CreateAnonymousPipe()
        {

        }

        public CreateAnonymousPipe(String pipeType, UInt32 id)
        {
            this.PipeType = pipeType;
            this.ID = id;
        }

        public String PipeType { get; set; }
        public UInt32 ID { get; set; }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<CreateAnonymousPipe>().PackSingleObject(this);
        }
        public override CreateAnonymousPipe Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<CreateAnonymousPipe>().Unpack(ms);
        }

        public override CreateAnonymousPipe Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<CreateAnonymousPipe>().UnpackSingleObject(buffer);
        }
    }

    /// <summary>
    /// RPC used to open an authenticated pipe with the server
    /// </summary>
    public class CreateAuthenticatedPipe : RPCBase<CreateAuthenticatedPipe>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.AuthenticatedPipe;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }

        public UInt64 ID { get; set; }
        public byte[] LongTermPubKey { get; set; }
        public byte[] Authenticator { get; set; }
        public CreateAuthenticatedPipe(String connectionType,
                                           UInt64 ID,
                                           byte[] longTermPK,
                                           byte[] authenticator)
        {
            this.ID = ID;
            LongTermPubKey = longTermPK;
            Authenticator = authenticator;
        }

        public CreateAuthenticatedPipe()
        {
            ID = UInt64.MaxValue;
            LongTermPubKey = new byte[] { };
            Authenticator = new byte[] { };
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<CreateAuthenticatedPipe>().PackSingleObject(this);
        }
        public override CreateAuthenticatedPipe Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<CreateAuthenticatedPipe>().Unpack(ms);
        }

        public override CreateAuthenticatedPipe Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<CreateAuthenticatedPipe>().UnpackSingleObject(buffer);
        }
    }


    /// <summary>
    /// RPC that requests a pipe be closed
    /// </summary>
    public class ClosePipeRPC : RPCBase<ClosePipeRPC>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.ClosePipe;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        public UInt32 ID { get; set; }
        public ClosePipeRPC(UInt32 id)
        {
            this.ID = id;
        }

        public ClosePipeRPC()
        {
            this.ID = UInt32.MaxValue;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<ClosePipeRPC>().PackSingleObject(this);
        }
        public override ClosePipeRPC Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<ClosePipeRPC>().Unpack(ms);
        }

        public override ClosePipeRPC Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<ClosePipeRPC>().UnpackSingleObject(buffer);
        }
    }

    /// <summary>
    /// RPC that acknowledges that a piep has been created
    /// </summary>
    public class AckPipe : RPCBase<AckPipe>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.AckPipe;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        public UInt32 ID { get; set; }
        public AckPipe()
        {
            ID = UInt32.MaxValue;
        }

        public AckPipe(UInt32 id)
        {
            this.ID = id;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<AckPipe>().PackSingleObject(this);
        }
        public override AckPipe Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<AckPipe>().Unpack(ms);
        }

        public override AckPipe Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<AckPipe>().UnpackSingleObject(buffer);
        }
    }

    public class RefusePipe : RPCBase<RefusePipe>, IHasSerializationTag
    {
        public enum RefusalReason
        {
            ID_ALREADY_EXISTS = 0,
            CANNOT_OPEN_ANOTHER_CONTROL = 253,
            UNSUPPORTED_PIPE_TYPE = 254,
            UNKNOWN = 255
        }

        protected const byte cSerializationTag = (byte)RPCType.RefusePipe;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }


        public UInt32 ID { get; set; }
        public byte Reason { get; set; }
        public RefusePipe(UInt32 id, RefusalReason reason)
        {
            this.ID = id;
            this.Reason = (byte)reason;
        }

        public RefusePipe()
        {

        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<RefusePipe>().PackSingleObject(this);
        }
        public override RefusePipe Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<RefusePipe>().Unpack(ms);
        }

        public override RefusePipe Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<RefusePipe>().UnpackSingleObject(buffer);
        }
    }

    public class RequestCertificate : RPCBase<RequestCertificate>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.RequestCertificate;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        public bool Request { get; set; }
        public RequestCertificate()
        {
            this.Request = true;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<RequestCertificate>().PackSingleObject(this);
        }
        public override RequestCertificate Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<RequestCertificate>().Unpack(ms);
        }

        public override RequestCertificate Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<RequestCertificate>().UnpackSingleObject(buffer);
        }
    }

    public class GiveCertificate : RPCBase<GiveCertificate>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.RequestCertificate;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        public byte[] Certificate { get; set; }
        public GiveCertificate(byte[] cert)
        {
            this.Certificate = cert;
        }

        public GiveCertificate()
        {

        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<GiveCertificate>().PackSingleObject(this);
        }
        public override GiveCertificate Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<GiveCertificate>().Unpack(ms);
        }

        public override GiveCertificate Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<GiveCertificate>().UnpackSingleObject(buffer);
        }
    }

    public class OkRPC : RPCBase<OkRPC>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.Ok;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        /// <summary>
        /// ID of the RPC we're oking
        /// </summary>
        public UInt32 RPCID { get; set; }

        public OkRPC()
        {

        }
        public OkRPC(UInt32 id)
        {
            RPCID = id;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<OkRPC>().PackSingleObject(this);
        }
        public override OkRPC Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<OkRPC>().Unpack(ms);
        }

        public override OkRPC Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<OkRPC>().UnpackSingleObject(buffer);
        }
    }

    public class NextTID : RPCBase<NextTID>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.NextTID;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        public NextTID(UInt64 tid)
        {
            this.TID = tid;
        }

        public UInt64 TID { get; set; }

        public NextTID()
        {
            Random rand = new Random();
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            UInt64 longRand = BitConverter.ToUInt64(buf, 0);
            this.TID = (longRand % (UInt64.MaxValue - 1)) + 1;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<NextTID>().PackSingleObject(this);
        }
        public override NextTID Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<NextTID>().Unpack(ms);
        }

        public override NextTID Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<NextTID>().UnpackSingleObject(buffer);
        }
    }

    public class RekeyNow : RPCBase<RekeyNow>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.RekeyNow;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }

        public bool Do { get; set; }

        public RekeyNow()
        {
            Do = true;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<RekeyNow>().PackSingleObject(this);
        }
        public override RekeyNow Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<RekeyNow>().Unpack(ms);
        }

        public override RekeyNow Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<RekeyNow>().UnpackSingleObject(buffer);
        }
    }

    public class PrepareRekey : RPCBase<PrepareRekey>, IHasSerializationTag
    {
        public override byte SerializationTag
        {
            get { return (byte)RPCType.PrepareRekey; }
        }

        public byte[] NextPublicKey { get; set; }

        public PrepareRekey(byte[] publicKey)
        {
            this.NextPublicKey = publicKey;
        }

        public PrepareRekey()
        {

        }

        public override PrepareRekey Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<PrepareRekey>().Unpack(ms);
        }

        public override PrepareRekey Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<PrepareRekey>().UnpackSingleObject(buffer);
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<PrepareRekey>().PackSingleObject(this);
        }
    }

    public class RekeyResponse : RPCBase<RekeyResponse>, IHasSerializationTag
    {
        public override byte SerializationTag
        {
            get { return (byte)RPCType.RekeyResponse; }
        }

        public byte[] NextPublicKey { get; set; }

        public RekeyResponse(byte[] publicKey)
        {
            this.NextPublicKey = publicKey;
        }

        public RekeyResponse()
        {

        }

        public RekeyResponse(PrepareRekey rekey)
        {
            this.NextPublicKey = rekey.NextPublicKey;
        }

        public override RekeyResponse Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<RekeyResponse>().Unpack(ms);
        }

        public override RekeyResponse Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<RekeyResponse>().UnpackSingleObject(buffer);
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<RekeyResponse>().PackSingleObject(this);
        }
    }

    public class PosePuzzle : RPCBase<PosePuzzle>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.NextTID;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }

        public bool Do { get; set; }

        public PosePuzzle()
        {
            Do = true;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<PosePuzzle>().PackSingleObject(this);
        }
        public override PosePuzzle Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<PosePuzzle>().Unpack(ms);
        }

        public override PosePuzzle Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<PosePuzzle>().UnpackSingleObject(buffer);
        }
    }

    public class ProvideSolution : RPCBase<ProvideSolution>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.NextTID;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }

        public bool Do { get; set; }

        public ProvideSolution()
        {
            Do = true;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<ProvideSolution>().PackSingleObject(this);
        }
        public override ProvideSolution Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<ProvideSolution>().Unpack(ms);
        }

        public override ProvideSolution Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<ProvideSolution>().UnpackSingleObject(buffer);
        }
    }

    public class ResizeWindow : RPCBase<ResizeWindow>
    {
        protected const byte cSerializationTag = (byte)RPCType.WindowResize;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }

        public bool Do { get; set; }

        public ResizeWindow()
        {
            Do = true;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<ResizeWindow>().PackSingleObject(this);
        }
        public override ResizeWindow Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<ResizeWindow>().Unpack(ms);
        }

        public override ResizeWindow Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<ResizeWindow>().UnpackSingleObject(buffer);
        }
    }

    public class Refuse : RPCBase<Refuse>, IHasSerializationTag
    {
        protected const byte cSerializationTag = (byte)RPCType.Refuse;
        public override byte SerializationTag
        {
            get
            {
                return cSerializationTag;
            }
        }
        /// <summary>
        /// ID of the RPC we're oking
        /// </summary>
        public UInt32 RPCID { get; set; }

        public Refuse()
        {

        }
        public Refuse(UInt32 id)
        {
            RPCID = id;
        }

        public override byte[] Serialize()
        {
            return MessagePackSerializer.Get<Refuse>().PackSingleObject(this);
        }
        public override Refuse Deserialize(MemoryStream ms)
        {
            return MessagePackSerializer.Get<Refuse>().Unpack(ms);
        }

        public override Refuse Deserialize(byte[] buffer)
        {
            return MessagePackSerializer.Get<Refuse>().UnpackSingleObject(buffer);
        }
    }
}
