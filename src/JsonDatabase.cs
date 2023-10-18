﻿using System;
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
        /// <summary>
        /// Deletes the whole data set.
        /// </summary>
        Delete      = (1 << 0),

        /// <summary>
        /// Inserts the data into a row.
        /// </summary>
        Insert      = (1 << 1),

        /// <summary>
        /// Changes a row.
        /// </summary>
        Change      = (1 << 2),
    }

    public class ModificationQuery
    {
        public string table;
        public ModificationQueryType type;
        public List<string> keys;
        public FindDataPredicate predicate;
        public List<object> data;
    }

    public enum ModificationFlags
    {
        Created     = (1 << 0),
        Deleted     = (1 << 1),
        Modified    = (1 << 2)
    }

    internal class JsonDatabaseTable
    {
        public JsonDatabaseTable() { }

        public JsonDatabaseTable(JsonDatabaseTable t)
        {
            Name = t.Name;
            Keys = t.Keys;
            Rows = t.Rows;
        }

        public JsonDatabaseTable(string name, List<string> keys, List<List<object>> rows)
        {
            Name = name;
            Keys = keys;
            Rows = rows;
        }

        [JsonProperty("n")] public string Name { get; set; }
        [JsonProperty("k")] public List<string> Keys { get; set; }
        [JsonProperty("r")] public List<List<object>> Rows { get; set; }
    }

    internal class JsonDatabase
    {
        public JsonDatabase() { }

        public JsonDatabase(JsonDatabase d)
        {
            Name = d.Name;
            CreatedAt = d.CreatedAt;
            LastUpdatedAt = d.LastUpdatedAt;
            Tables = d.Tables;
        }

        public JsonDatabase(string name, DateTime createdAt, DateTime lastUpdatedAt, List<JsonDatabaseTable> tables)
        {
            Name = name;
            CreatedAt = createdAt;
            LastUpdatedAt = lastUpdatedAt;
            Tables = tables;
        }

        [JsonProperty("n")] public string Name { get; set; }
        [JsonProperty("ca")] public DateTime CreatedAt { get; set; }
        [JsonProperty("lua")] public DateTime LastUpdatedAt { get; set; }
        [JsonProperty("t")] public List<JsonDatabaseTable> Tables { get; set; }
    }
}
