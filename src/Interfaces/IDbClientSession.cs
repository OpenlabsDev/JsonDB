using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonDb.Interfaces
{
    /// <summary>
    /// A session of <see cref="IDbClient{TFile}"/>
    /// </summary>
    public interface IDbClientSession
    {
        /// <summary>
        /// A unique ID of the session.
        /// </summary>
        string SessionId { get; }
    }
}
