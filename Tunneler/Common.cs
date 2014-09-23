using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tunneler
{
    public class Common
    {
        public const UInt64 PUBLIC_KEY_FLAG = (1LU << 63);
        public const UInt64 PUZZLE_FLAG = (1LU << 62);
        public const UInt64 TID_FLAGS = (PUBLIC_KEY_FLAG | PUZZLE_FLAG);
        public const byte START_PAYLOAD_FLAG = 1;

        /// <summary>
        /// Removes the TID flags the specify if that packet
        /// has a puzzle/solution or has a Euphemeral Public Key
        /// </summary>
        /// <returns>The TID flags.</returns>
        /// <param name="tid">Tid.</param>
        public static UInt64 RemoveTIDFlags(UInt64 tid)
        {
            return tid & ~TID_FLAGS;
        }

        /// <summary>
        /// Determines if the TID has an EPK flag
        /// </summary>
        /// <returns><c>true</c>, if has EP was TIDed, <c>false</c> otherwise.</returns>
        /// <param name="tid">Tid.</param>
        public static bool TIDHasEPK(UInt64 tid)
        {
            return (tid & PUBLIC_KEY_FLAG) == PUBLIC_KEY_FLAG;
        }

        /// <summary>
        /// Determines if the TID has a Puzzle flag
        /// </summary>
        /// <returns><c>true</c>, if has puzzle flag was TIDed, <c>false</c> otherwise.</returns>
        /// <param name="tid">Tid.</param>
        public static bool TIDHasPuzzleFlag(UInt64 tid)
        {
            return (tid & PUZZLE_FLAG) == PUZZLE_FLAG;
        }
    }
}
