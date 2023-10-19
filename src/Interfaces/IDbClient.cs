using JsonDb.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonDb
{
    /// <summary>
    /// A boiler-plate for a JsonDB client.
    /// </summary>
    public interface IDbClient<TFile, TSession> 
        where TFile : IDbFile 
        where TSession : IDbClientSession
    {
        /// <summary>
        /// Initializes the client to latch (or open) into a file.
        /// </summary>
        /// <param name="file">The database file to use.</param>
        TSession Open(string sessionId = null);

        /// <summary>
        /// Closes the client.
        /// </summary>
        void Close(TSession session);

        /// <summary>
        /// Is the client using a async operation setup?
        /// </summary>
        bool IsAsync { get; }
    }
}
