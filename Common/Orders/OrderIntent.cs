using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Order Intention - simplifies management of open orders
    /// </summary>
    public enum OrderIntent
    {
        /// <summary>
        /// Undefined
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Buy To Open
        /// </summary>
        BTO = 1,

        /// <summary>
        /// Sell To Open
        /// </summary>
        STO = 2,

        /// <summary>
        /// Sell Short (Equities)
        /// </summary>
        SSHORT = 3,

        /// <summary>
        /// Sell To Close
        /// </summary>
        STC = 4,
        /// <summary>
        /// Buy to Close
        /// </summary>
        BTC = 5,
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 6
    }
}
