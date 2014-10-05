using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tunneler.Packet;

namespace TunnelerTestWin.mocks
{
    class GenericPacketMock:GenericPacket
    {
		private byte[] payload; 
        public GenericPacketMock(UInt32 seq)
        {
            this.Ack = 0;
            this.Seq = seq;
        }

		public void SetPayload(byte[] payload){
			this.payload = payload;
		}

		public byte[] GetPayload(){
			return payload;
		}
    }
}
