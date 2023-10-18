using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Tiny;

namespace JsonDb
{
    /// <summary>
    /// Represents a .jsondb file.
    /// </summary>
    public class JsonDbFile : IDbFile
    {
        public string Name { get; }

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
        public JsonDbFile(string name, string path)
        {
            Name = name;
            Path = path;
        }

        internal JsonDatabase OpenDb()
        {
            if (Open) return null;
            Open = true;

            if (!File.Exists(Path))
            {
                SaveDb(new JsonDatabase()
                {
                    Name = Name,
                    CreatedAt = DateTime.Now,
                    LastUpdatedAt = DateTime.Now,
                    Tables = new List<JsonDatabaseTable>()
                });
            }

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
