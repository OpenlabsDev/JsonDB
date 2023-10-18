using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonDb
{
    /// <summary>
    /// A boiler-plate for a JsonDB file.
    /// </summary>
    public interface IDbFile
    {
        /// <summary>
        /// The path to the file.
        /// </summary>
        string Path { get; }
    }
}
