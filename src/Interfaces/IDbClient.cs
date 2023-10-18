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
    public interface IDbClient<TFile> where TFile : IDbFile
    {
        /// <summary>
        /// Initializes the client to latch (or open) into a file.
        /// </summary>
        /// <param name="file">The database file to use.</param>
        void Open(TFile file);

        /// <summary>
        /// Closes the client.
        /// </summary>
        void Close();
    }
}
