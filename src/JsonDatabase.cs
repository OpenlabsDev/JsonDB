using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tiny;

// internal classes that are decoded from JSON.
namespace JsonDb
{
    public enum ModificationQueryType
    {
        Create      = (1 << 0),
        Delete      = (1 << 1),
        Insert      = (1 << 2),
        Change      = (1 << 3),
    }

    public class ModificationQuery
    {
        public string table;
        public ModificationQueryType type;
        public List<object> data;
    }

    public enum ModificationFlags
    {
        Created     = (1 << 0),
        Deleted     = (1 << 1),
        Modified    = (1 << 2)
    }

    internal class JsonDatabaseModification
    {
        [JsonProperty("sid")] public string SessionId { get; set; }
        [JsonProperty("mf")] public ModificationFlags Modification { get; set; }
    }

    internal class JsonDatabaseTable
    {
        [JsonProperty("n")] public string Name { get; set; }
        [JsonProperty("r")] public List<string> Rows { get; set; }
        [JsonProperty("c")] public List<List<object>> Columns { get; set; }
    }

    internal class JsonDatabase
    {
        [JsonProperty("n")] public string Name { get; set; }
        [JsonProperty("ca")] public DateTime CreatedAt { get; set; }
        [JsonProperty("lua")] public DateTime LastUpdatedAt { get; set; }
        [JsonProperty("mh")] public List<JsonDatabaseModification> ModificationHistory { get; set; }
        [JsonProperty("t")] public List<JsonDatabaseTable> Tables { get; set; }
    }
}
