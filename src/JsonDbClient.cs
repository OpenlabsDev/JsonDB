using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Tiny;

namespace JsonDb
{
    /// <summary>
    /// A predicate used to find data in rows.
    /// </summary>
    /// <param name="values">The row of the data.</param>
    public delegate bool FindDataPredicate(List<object> values);

    /// <summary>
    /// Represents a client that can modify a .jsondb file.
    /// </summary>
    public class JsonDbClient : IDbClient<JsonDbFile>
    {
        /// <inheritdoc/>
        public void Open(JsonDbFile file)
        {
            _session = Guid.NewGuid().ToString();
            _file = file;

            _db = _file.OpenDb();
        }

        private ModificationFlags GetFlagFromModificationType(ModificationQueryType queryType)
        {
            if (queryType == ModificationQueryType.Delete)
                return ModificationFlags.Deleted;
            else if (queryType == ModificationQueryType.Insert)
                return ModificationFlags.Modified;
            else if (queryType == ModificationQueryType.Change)
                return ModificationFlags.Modified;

            throw new Exception("Invalid modification type given");
        }

        /// <summary>
        /// Creates a table if it doesnt exist.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="keys">The keys to include.</param>
        /// <param name="rows">The data to initialize the table with.</param>
        public bool InitTable(string tableName, List<string> keys, List<List<object>> rows)
        {
            if (!_db.Tables.Any(x => x.Name == tableName))
            {
                _db.Tables.Add(new JsonDatabaseTable
                {
                    Name = tableName,
                    Keys = keys,
                    Rows = rows
                });

                _file.SaveDb(_db);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Executes a modification query, and then saves the file.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        public bool ModifyQuery(ModificationQuery query)
        {
            try
            {
                var table = _db.Tables.Find(x => x.Name == query.table);
                var tableIdx = _db.Tables.IndexOf(table);

                if (table == null || tableIdx <= -1)
                    return false;

                if (query.type == ModificationQueryType.Insert)
                {
                    if (query.data.Count != table.Keys.Count)
                        throw new Exception("Invalid query signature -- incorrect data given to an insert query.");
                    table.Rows.Add(query.data);

                    _db.Tables[tableIdx] = table;
                    _file.SaveDb(_db);
                }
                else if (query.type == ModificationQueryType.Delete)
                {
                    List<int> indexes = new List<int>();
                    foreach (var key in table.Keys)
                        if (query.keys.Contains(key))
                        {
                            var idx = table.Keys.IndexOf(key);
#if LOG_MESSAGES
                            Console.WriteLine("INFO: adding " + key + "; idx = " + idx);
#endif
                            indexes.Add(idx);
                        }

                    int rowToDelete = -1;
                    for (int x = 0; x < table.Keys.Count; x++)
                        for (int y = 0; y < table.Rows.Count; y++)
                        {
#if LOG_MESSAGES
                            Console.WriteLine("INFO: running predicate for  " + Json.Encode(table.Rows[y]));
#endif
                            if (indexes.Contains(x) && query.predicate(table.Rows[y]))
                            {
#if LOG_MESSAGES
                                Console.WriteLine("INFO: deleting row " + y);
#endif
                                rowToDelete = y;
                            }
                        }
                    table.Rows.RemoveAt(rowToDelete);

                    _db.Tables[tableIdx] = table;
                    _file.SaveDb(_db);
                }
                else if (query.type == ModificationQueryType.Change)
                {
                    List<(int keyIdx, int pendingChangeIdx)> indexes = new();
                    foreach (var key in table.Keys)
                        if (query.keys.Contains(key))
                        {
                            var keyIdx = table.Keys.IndexOf(key);
                            var pendingChangeIdx = query.keys.IndexOf(key);

#if LOG_MESSAGES
                            Console.WriteLine("INFO: adding " + key + "; keyIdx = " + keyIdx + "; pendingChangeIdx = " + pendingChangeIdx);
#endif
                            indexes.Add((keyIdx, pendingChangeIdx));
                        }

                    List<Tuple<int, int, object>> pendingChanges = new();
                    //
                    // named x and y because rows and columns are in a vector format
                    //
                    //          <-- [x] -->                            
                    //    0      1      2      3        
                    // =------=------=------=------=    
                    // =  ID  = Name = Desc = Date =  0 
                    // =------=------=------=------=    /\ 
                    // =  1   = Test = Test = None =  1  |
                    // =------=------=------=------=    [y]
                    // =  2   = Test = Test = None =  2  |
                    // =------=------=------=------=    \/
                    // =  3   = Test = Test = None =  3
                    // =------=------=------=------=
                    //
                    for (int x = 0; x < table.Keys.Count; x++)
                        for (int y = 0; y < table.Rows.Count; y++)
                        {
#if LOG_MESSAGES
                            Console.WriteLine("INFO: running predicate for  " + Json.Encode(table.Rows[y]));
#endif
                            if (indexes.Any(o => o.keyIdx == x) && query.predicate(table.Rows[y]))
                            {
                                var idxInfo = indexes.Find(o => o.keyIdx == x);

#if LOG_MESSAGES
                                Console.WriteLine("INFO: pred passed; query.data.Count = " + query.data.Count + "; idxInfo.pendingChangeIdx = " + idxInfo.pendingChangeIdx);
#endif
                                pendingChanges.Add(new Tuple<int, int, object>(x, y, query.data[idxInfo.pendingChangeIdx]));
                            }
                        }

#if LOG_MESSAGES
                    Console.WriteLine("INFO: finished initial table evaluation");
#endif

                    foreach (var change in pendingChanges)
                    {
#if LOG_MESSAGES
                        Console.WriteLine("INFO: changing " + Json.Encode(table.Rows[change.Item2][change.Item1]) + " --> " + Json.Encode(change.Item3));
#endif
                        table.Rows[change.Item2][change.Item1] = change.Item3;
                    }

                    _db.Tables[tableIdx] = table;
                    _file.SaveDb(_db);
                }

                return true;
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            finally
            {
                _file.SaveDb(_db);
            }
        }

        private List<object> GetIncludedColumns(List<int> requestedIndexes, List<object> allValues)
        {
            List<object> result = new List<object>();
            for (int i = 0; i < requestedIndexes.Count; i++)
                for (int j = 0; j < allValues.Count; j++)
                    if (requestedIndexes.Contains(i) && requestedIndexes.Contains(j))
                        result.Add(allValues[j]);
            return result;
        }

        /// <summary>
        /// Requests data from a row.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="keys">The keys to include.</param>
        /// <param name="predicate">The predicate to check if the the data is correct.</param>
        /// <param name="data">The data returned by the operation.</param>
        public bool GetQuery(string tableName, List<string> keys, FindDataPredicate predicate, out List<object> data)
        {
            data = new List<object>();

            foreach (var table in _db.Tables)
            {
                List<int> indexes = new List<int>();
                foreach (var key in table.Keys)
                    if (keys.Contains(key))
                        indexes.Add(table.Keys.IndexOf(key));

                if (table.Name == tableName)
                    foreach (var row in table.Rows)
                    {
                        var requestedColumns = GetIncludedColumns(indexes, row);
                        if (predicate(requestedColumns))
                        {
                            data = requestedColumns;
                            break;
                        }
                    }
            }
            return true;
        }

        /// <inheritdoc/>
        public void Close() 
        {
            _file = null;
            _db = null;
        }

        private string _session;
        private JsonDbFile _file;
        private JsonDatabase _db;
    }
}
