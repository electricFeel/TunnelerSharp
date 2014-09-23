using System;
using Tunneler;
using Tunneler.Packet;

namespace TunnelerTestWin.mocks
{
    class TunnelSocketMock : TunnelSocket
    {
        private Action<GenericPacket> packetHandle;

        public TunnelSocketMock()
        {
            //don't start it
        }

        public void InterceptPacket(Action<GenericPacket> handle)
        {
            this.packetHandle = handle;
        }

        public override void SendPacket(GenericPacket p)
        {
            if (this.packetHandle != null)
            {
                this.packetHandle.Invoke(p);
            }
        }

        public new void  HandlePacket(EncryptedPacket p)
        {
            if (mTunnelDirectory.TunnelIDExists(p.TID))
            {
                TunnelBase tunnel;
                mTunnelDirectory.Get(p.TID, out tunnel);
                tunnel.HandleIncomingPacket(p);
            }
        }


    }
}