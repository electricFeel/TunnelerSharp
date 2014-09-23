using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using C5;

namespace Tunneler
{
    /// <summary>
    /// The tunnel runtime provides a global access point to see what tunnels have been created or assigned to specific ports
    /// within an application
    /// </summary>
    public sealed class TunnelRuntime
    {
        private static C5.TreeDictionary<short, TunnelSocket> sTunnels;

        static TunnelRuntime()
        {
            sTunnels = new TreeDictionary<short, TunnelSocket>();
            //when the application is about to shutdown we should free the open
            //connections
            AppDomain.CurrentDomain.DomainUnload += (object sender, EventArgs e) =>
            {
                foreach (short port in sTunnels.Keys)
                {
                    TunnelSocket sock;
                    short refPort = port;
                    sTunnels.Find(ref refPort, out sock);
                    sock.Close();
                }
            };
        }

        public static TunnelSocket GetOrCreateTunnelSocket(short port)
        {
            TunnelSocket b;
            short p = port;
            if (sTunnels.Find(ref p, out b))
            {
                return b;
            }
            else
            {
                b = new TunnelSocket(port);
                b.Start();
            }
            return b;
        }

        internal static void ClostTunnelSocket(TunnelSocket tunnelSocket)
        {
            tunnelSocket.Close();
            sTunnels.Remove(tunnelSocket.Port);
        }


    }
}
