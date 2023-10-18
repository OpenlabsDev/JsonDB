using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tiny;

namespace JsonDb
{
    /// <summary>
    /// Represents a .jsondb file.
    /// </summary>
    public class JsonDbFile : IDbFile
    {
        /// <inheritdoc/>
        public string Path { get; }

        /// <summary>
        /// A boolean which shows if the database is open and ready for changes.
        /// </summary>
        public bool Open { get; private set; }

        /// <summary>
        /// Creates a new instance of <see cref="JsonDbFile"/>
        /// </summary>
        /// <param name="path">The path to the json database file.</param>
        public JsonDbFile(string path)
        {
            Path = path;
        }

        internal JsonDatabase OpenDb()
        {
            if (Open) return null;
            Open = true;

            if (!File.Exists(Path))
                throw new Exception("The file does not exist.");

            var contents = Encoding.UTF8.GetString(
                Convert.FromBase64String(File.ReadAllText(Path))
            );
            return Tiny.Json.Decode<JsonDatabase>(contents);
        }

        internal void SaveDb(JsonDatabase database)
        {
            if (!Open) return;

            var contents = Encoding.UTF8.GetBytes(Tiny.Json.Encode(database));
            File.WriteAllText(Path, Convert.ToBase64String(contents));
        } 

        internal void CloseDb()
        {
            if (!Open) return;
            Open = false;
        }
    }
}
