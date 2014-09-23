using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tunneler;
using Tunneler.Packet;
using Tunneler.Pipe;

namespace TunnelerTestWin.mocks
{
    internal class TunnelMock : SecureTunnel
    {
        private Action<EncryptedPacket> packetHandle;
        private Action<PipeBase> connectionHandle;
        private Action<byte[]> rekeyHandle;
        private UInt16 mtuSize;
        public TunnelMock(TunnelSocket socket)
            : base(socket)
        {
        }

        public void SetMTUSize(UInt16 mtuSize)
        {
            this.mtuSize = mtuSize;
        }

        public void PacketInterceptor(Action<EncryptedPacket> handle)
        {
            this.packetHandle = handle;
        }

        public void PipeInterceptor(Action<PipeBase> handle)
        {
            this.connectionHandle = handle;
        }

        public void RekeyInterceptor(Action<byte[]> handle)
        {
            this.rekeyHandle = handle;
        }

        public override bool OpenPipe(PipeBase connection)
        {
            this.ActivePipes.Add(connection.ID, connection);
            var handle = this.connectionHandle;
            if (handle != null) handle.Invoke(connection);
            return true;
        }

        public override bool ClosePipe(uint id)
        {
            PipeBase connection;
            if (this.ActivePipes.Find(ref id, out connection))
            {
                var handle = this.connectionHandle;
                if (handle != null) handle.Invoke(connection);
                return true;
            }
            else
            {
                throw new Exception("Cannot find connection");
            }
        }

        public override void SetNextRecipentPublicKey(byte[] key)
        {
            if (rekeyHandle != null) rekeyHandle.Invoke(key);
            base.SetNextRecipentPublicKey(key);
        }


        public override void EncryptAndSendPacket(EncryptedPacket p)
        {
            var handle = this.packetHandle;
            if (handle != null) handle.Invoke(p);
        }

        public override ushort GetMaxPayloadSize()
        {
            return this.mtuSize;
        }
    }
}
