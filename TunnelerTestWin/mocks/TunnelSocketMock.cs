using System;
using Tunneler;
using Tunneler.Packet;

namespace TunnelerTestWin.mocks
{
    class TunnelSocketMock : TunnelSocket
    {
        private Action<GenericPacket> packetOutHandle;
        private Action<GenericPacket> packetInHandle;

        public TunnelSocketMock()
        {
            //don't start it
        }

        public void InterceptOutgoingPacket(Action<GenericPacket> handle)
        {
            this.packetOutHandle = handle;
        }

        public void InterceptIncomingPacket(Action<GenericPacket> handle)
        {
            this.packetInHandle = handle;
        }

        public override void SendPacket(GenericPacket p)
        {
            if (this.packetOutHandle != null)
            {
                this.packetOutHandle.Invoke(p);
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

        public void HandlePacket(GenericPacket p)
        {
            if (this.packetInHandle != null)
            {
                this.packetInHandle.Invoke(p);
            }
        }
    }
}