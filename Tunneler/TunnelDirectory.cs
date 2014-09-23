using System;
using System.Linq;
using System.Text;
using System.Threading;
using C5;

namespace Tunneler
{
    /// <summary>
    /// The abstractTunnel directory is a fast lookup to get a abstractTunnel
    /// instance from a abstractTunnel id. It also provides a fast check
    /// to see if a abstractTunnel exists. It's associated with a TunnelSocket. This
    /// lookup class is threadsafe.
    /// </summary>
    public class TunnelDirectory
    {
        private const int READER_LOCK_TIMEOUT_MS = 5;
        private const int WRITER_LOCK_TIMEOUT_MS = 10;
        private ReaderWriterLock lReadWriteLock = new ReaderWriterLock();
        private TreeDictionary<UInt64, TunnelBase> mTunnels { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelDirectory"/> class. Each
        /// abstractTunnel socket should maintain an instance of this.
        /// </summary>
        internal TunnelDirectory()
        {
            this.mTunnels = new TreeDictionary<UInt64, TunnelBase>();
        }

        /// <summary>
        /// Checks to see if a TID already exists.
        /// </summary>
        /// <returns><c>true</c>, if ID exists, <c>false</c> otherwise.</returns>
        /// <param name="tid">The abstractTunnel id.</param>
        public bool TunnelIDExists(UInt64 tid)
        {
            lReadWriteLock.AcquireReaderLock(READER_LOCK_TIMEOUT_MS);
            TunnelBase abstractTunnel;
            bool result = false;
            if (this.mTunnels.Find(ref tid, out abstractTunnel))
            {
                result = true;
            }
            lReadWriteLock.ReleaseReaderLock();
            return result;
        }

        /// <summary>
        /// Will insert a abstractTunnel into the abstractTunnel directory.
        /// </summary>
        /// <returns><c>true</c>, if abstractTunnel was inserted, <c>false</c> otherwise.</returns>
        /// <param name="abstractTunnel">SecureAbstractTunnel.</param>
        public bool InsertTunnel(TunnelBase abstractTunnel)
        {
            bool result = false;
            lReadWriteLock.AcquireWriterLock(WRITER_LOCK_TIMEOUT_MS);
            //todo: determine if it makes sense to do a check here or to have the inserter do 
            //the check to see if the TID already exists
            this.mTunnels.Add(abstractTunnel.ID, abstractTunnel);
            result = true;
            lReadWriteLock.ReleaseWriterLock();
            return result;
        }

        public bool InsertTunnel(TunnelBase abstractTunnel, UInt64 id)
        {
            bool result = false;
            lReadWriteLock.AcquireWriterLock(WRITER_LOCK_TIMEOUT_MS);
            //todo: determine if it makes sense to do a check here or to have the inserter do 
            //the check to see if the TID already exists
            this.mTunnels.Add(id, abstractTunnel);
            result = true;
            lReadWriteLock.ReleaseWriterLock();
            return result;
        }

        /// <summary>
        /// Removes the abstractTunnel.
        /// </summary>
        /// <returns><c>true</c>, if abstractTunnel was removed, <c>false</c> otherwise.</returns>
        /// <param name="abstractTunnel">SecureAbstractTunnel.</param>
        public bool RemoveTunnel(TunnelBase abstractTunnel)
        {
            return this.RemoveTunnel(abstractTunnel.ID);
        }

        /// <summary>
        /// Removes the abstractTunnel with the associated abstractTunnel id.
        /// </summary>
        /// <returns><c>true</c>, if abstractTunnel was removed, <c>false</c> otherwise.</returns>
        /// <param name="TID">TI.</param>
        public bool RemoveTunnel(UInt64 TID)
        {
            bool result = false;
            lReadWriteLock.AcquireWriterLock(WRITER_LOCK_TIMEOUT_MS);
            //todo: determine if it makes sense to do a check here or to have the inserter do 
            //the check to see if the TID already exists
            this.mTunnels.Remove(TID);
            result = true;
            lReadWriteLock.ReleaseWriterLock();
            return result;
        }

        /// <summary>
        /// Get the specified TID and t. Returns false if it isn't found.
        /// </summary>
        /// <param name="TID">TI.</param>
        /// <param name="t">T.</param>
        public bool Get(UInt64 TID, out TunnelBase t)
        {
            lReadWriteLock.AcquireReaderLock(READER_LOCK_TIMEOUT_MS);
            bool ret = this.mTunnels.Find(ref TID, out t);
            lReadWriteLock.ReleaseLock();
            return ret;
        }

        public void CloseAllTunnels()
        {
            this.lReadWriteLock.AcquireWriterLock(30);
            foreach (C5.KeyValuePair<ulong, TunnelBase> t in this.mTunnels)
            {
                t.Value.CloseCommunications();
            }
            this.lReadWriteLock.ReleaseLock();
        }

        public IList<UInt64> GetIDs()
        {
            IList<UInt64> keys = new ArrayList<UInt64>();
            this.lReadWriteLock.AcquireReaderLock(30);
            keys.AddAll(this.mTunnels.Keys.ToArray());
            this.lReadWriteLock.ReleaseLock();
            return keys;
        }
    }
}
