﻿#if DEBUG
#define LOG_MESSAGES
#endif

using JsonDb.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace JsonDb
{
    /// <summary>
    /// A predicate used to find data in rows.
    /// </summary>
    /// <param name="values">The row of the data.</param>
    public delegate bool FindDataPredicate(List<object> values);

    public class InitTableArgs
    {
        public InitTableArgs(string tableName, List<string> keys, List<List<object>> rows)
        {
            TableName = tableName;
            Keys = keys;
            Rows = rows;
        }

        public string TableName { get; }
        public List<string> Keys { get; }
        public List<List<object>> Rows { get; }

        public Action OnSuccess { get; set; } = new(() => { });
        public Action<string> OnError { get; set; } = new(err => { });
    }

    public class ModifyQueryArgs
    {
        public ModifyQueryArgs(ModificationQuery query)
        {
            Query = query;
        }

        public ModificationQuery Query { get; }

        public Action OnSuccess { get; set; } = new(() => { });
        public Action<string> OnError { get; set; } = new(err => { });
    }

    public class GetQueryArgs
    {
        public GetQueryArgs(string tableName, List<string> keys, FindDataPredicate predicate)
        {
            TableName = tableName;
            Keys = keys;
            Predicate = predicate;
        }

        public string TableName { get; }
        public List<string> Keys { get; }
        public FindDataPredicate Predicate { get; }

        public Action<List<object>> OnSuccess { get; set; } = new(x => { });
        public Action<string> OnError { get; set; } = new(err => { });
    }


    public class GetAllQueryArgs
    {
        public GetAllQueryArgs(string tableName, List<string> keys)
        {
            this.TableName = tableName;
            this.Keys = keys;
        }

        public string TableName { get; }

        public List<string> Keys { get; }

        public Action<List<List<object>>> OnSuccess { get; set; } = new(list => { });
        public Action<string> OnError { get; set; } = new(err => { });
    }

    /// <summary>
    /// A session of <see cref="JsonDbClient"/>
    /// </summary>
    public sealed class JsonDbClientSession : IDbClientSession
    {
        /// <inheritdoc/>
        public string SessionId { get; }

        internal JsonDbClientSession(string sessionId, JsonDbClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            SessionId = sessionId;
        }

        ~JsonDbClientSession() 
        {
            _client.File.SaveDb(_client.Database);
            _client.File.CloseDb();
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
        public bool InitTable(InitTableArgs args)
        {
            try
            {
                if (!_client.Database.Tables.Any(x => x.Name == args.TableName))
                {
                    _client.Database.Tables.Add(new JsonDatabaseTable
                    {
                        Name = args.TableName,
                        Keys = args.Keys,
                        Rows = args.Rows
                    });

#if LOG_MESSAGES
                    Console.WriteLine("INFO: adding " + args.TableName + " table to db");
#endif
                    _client.File.SaveDb(_client.Database);
                    if (args.OnSuccess != null) args.OnSuccess();
                    return true;
                }


#if LOG_MESSAGES
                Console.WriteLine("WARNING: table " + args.TableName + " already exists in db");
#endif
                if (args.OnError != null) args.OnError("Table already exists");
                return false;
            }
            catch (Exception ex)
            {
                if (args.OnError != null) args.OnError(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Executes a modification query, and then saves the file.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        public bool ModifyQuery(ModifyQueryArgs args)
        {
            try
            {
                var table = _client.Database.Tables.Find(x => x.Name == args.Query.table);
                var tableIdx = _client.Database.Tables.IndexOf(table);

                if (table == null || tableIdx <= -1)
                {
                    if (args.OnError != null) args.OnError("Table doesnt exist");
                    return false;
                }

                if (args.Query.type == ModificationQueryType.Insert)
                {
                    if (args.Query.data.Count != table.Keys.Count)
                        throw new Exception("Invalid query signature -- incorrect data given to an insert query.");
                    table.Rows.Add(args.Query.data);

                    _client.Database.Tables[tableIdx] = table;
                    _client.File.SaveDb(_client.Database);
                }
                else if (args.Query.type == ModificationQueryType.Delete)
                {
                    List<int> indexes = new List<int>();
                    foreach (var key in table.Keys)
                        if (args.Query.keys.Contains(key))
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
                            Console.WriteLine("INFO: running predicate for  " + JsonConvert.SerializeObject(table.Rows[y]));
#endif
                            if (indexes.Contains(x) && args.Query.predicate(table.Rows[y]))
                            {
#if LOG_MESSAGES
                                Console.WriteLine("INFO: deleting row " + y);
#endif
                                rowToDelete = y;
                            }
                        }
                    table.Rows.RemoveAt(rowToDelete);

                    _client.Database.Tables[tableIdx] = table;
                    _client.File.SaveDb(_client.Database);
                }
                else if (args.Query.type == ModificationQueryType.Change)
                {
                    List<(int keyIdx, int pendingChangeIdx)> indexes = new();
                    foreach (var key in table.Keys)
                        if (args.Query.keys.Contains(key))
                        {
                            var keyIdx = table.Keys.IndexOf(key);
                            var pendingChangeIdx = args.Query.keys.IndexOf(key);

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
                            Console.WriteLine("INFO: running predicate for  " + JsonConvert.SerializeObject(table.Rows[y]));
#endif
                            if (indexes.Any(o => o.keyIdx == x) && args.Query.predicate(table.Rows[y]))
                            {
                                var idxInfo = indexes.Find(o => o.keyIdx == x);

#if LOG_MESSAGES
                                Console.WriteLine("INFO: pred passed; query.data.Count = " + args.Query.data.Count + "; idxInfo.pendingChangeIdx = " + idxInfo.pendingChangeIdx);
#endif
                                pendingChanges.Add(new Tuple<int, int, object>(x, y, args.Query.data[idxInfo.pendingChangeIdx]));
                            }
                        }

#if LOG_MESSAGES
                    Console.WriteLine("INFO: finished initial table evaluation");
#endif

                    foreach (var change in pendingChanges)
                    {
#if LOG_MESSAGES
                        Console.WriteLine("INFO: changing " + JsonConvert.SerializeObject(table.Rows[change.Item2][change.Item1]) + " --> " + JsonConvert.SerializeObject(change.Item3));
#endif
                        table.Rows[change.Item2][change.Item1] = change.Item3;
                    }

                    _client.Database.Tables[tableIdx] = table;
                    _client.File.SaveDb(_client.Database);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (args.OnError != null) args.OnError(ex.Message);
                return false;
            }
            finally
            {
                _client.File.SaveDb(_client.Database);
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
        public bool GetQuery(GetQueryArgs args, out List<object> data)
        {
            data = new List<object>();


#if LOG_MESSAGES
            Console.WriteLine("INFO: getting data from the " + args.TableName + " table");
#endif
            for (int i = 0; i < _client.Database.Tables.Count; i++)
            {
                var table = _client.Database.Tables[i];
                List<int> indexes = new List<int>();
                foreach (var key in table.Keys)
                    if (args.Keys.Contains(key))
                        indexes.Add(table.Keys.IndexOf(key));

                if (table.Name == args.TableName)
                    foreach (var row in table.Rows)
                    {
                        var requestedColumns = GetIncludedColumns(indexes, row);
                        if (args.Predicate(requestedColumns))
                        {
                            data = requestedColumns;
                            break;
                        }
                    }
            }

#if LOG_MESSAGES
            Console.WriteLine("INFO: finished get operation in the " + args.TableName + " table");
#endif
            return true;
        }

        /// <summary>
        /// Requests all data from a row.
        /// </summary>
        public bool GetAllQuery(GetQueryArgs args, out List<List<object>> data)
        {
            data = new List<List<object>>();

            bool foundTable = false;
#if LOG_MESSAGES
            Console.WriteLine("INFO: getting data from the " + args.TableName + " table");
#endif
            for (int i = 0; i < _client.Database.Tables.Count; i++)
            {
                var table = _client.Database.Tables[i];
                List<int> indexes = new List<int>();
                foreach (var key in table.Keys)
                    if (args.Keys.Contains(key))
                        indexes.Add(table.Keys.IndexOf(key));

                if (table.Name == args.TableName)
                {
                    foundTable = true;
                    foreach (var row in table.Rows)
                    {
                        var requestedColumns = GetIncludedColumns(indexes, row);
                        if (args.Predicate(requestedColumns))
                        {
                            data.Add(requestedColumns);
                        }
                    }
                }
            }
            
            if (!foundTable)
            {
#if LOG_MESSAGES
                Console.WriteLine("WARNING: cannot find the " + args.TableName + " table");
#endif
                return false;
            }


#if LOG_MESSAGES
            Console.WriteLine("INFO: finished get operation in the " + args.TableName + " table");
#endif
            return true;
        }

        private JsonDbClient _client;
    }

    /// <summary>
    /// Represents a client that can modify a .jsondb file.
    /// </summary>
    public sealed class JsonDbClient : IDbClient<JsonDbFile, JsonDbClientSession>
    {
        /// <inheritdoc/>
        public bool IsAsync => _isAsync;

        internal JsonDatabase Database => _db;
        public JsonDbFile File => _file;

        /// <summary>
        /// Creates a new instance of <see cref="JsonDbClient"/>
        /// </summary>
        /// <param name="file">The database file to use.</param>
        /// <param name="async">Is any operation async?</param>
        public JsonDbClient(JsonDbFile file, bool async = true)
        {
            _file = file;
            _isAsync = async;
            _db = _file.OpenDb();

            if (async)
            {
                Task.Factory.StartNew(async () => await InitTable_QueueProcessor(), TaskCreationOptions.LongRunning);
                Task.Factory.StartNew(async () => await ModifyQuery_QueueProcessor(), TaskCreationOptions.LongRunning);
                Task.Factory.StartNew(async () => await GetQuery_QueueProcessor(), TaskCreationOptions.LongRunning);
                Task.Factory.StartNew(async () => await GetAllQuery_QueueProcessor(), TaskCreationOptions.LongRunning);
            }
        }

        /// <inheritdoc/>
        public JsonDbClientSession Open(string sessionId = null)
        {
            return GetOrCreateSession(sessionId ?? Guid.NewGuid().ToString());
        }

        #region Async Queues 
        public async Task InitTable_QueueProcessor()
        {
            for (; ; )
            {
                if (_initTableQueue.Count > 0)
                {
                    var args = _initTableQueue.Dequeue();

                    try
                    {
                        if (!_db.Tables.Any(x => x.Name == args.TableName))
                        {
                            _db.Tables.Add(new JsonDatabaseTable
                            {
                                Name = args.TableName,
                                Keys = args.Keys,
                                Rows = args.Rows
                            });

#if LOG_MESSAGES
                            Console.WriteLine("INFO: adding " + args.TableName + " table to db");
#endif

                            _file.SaveDb(_db);
                            if (args.OnSuccess != null) args.OnSuccess();
                        }
                        else
                        {
#if LOG_MESSAGES
                            Console.WriteLine("WARNING: table " + args.TableName + " already exists in db");
#endif

                            if (args.OnError != null) args.OnError("Table already exists");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (args.OnError != null) args.OnError(ex.Message);
                    }
                }

                await Task.Delay(25);
            }
        }

        public async Task ModifyQuery_QueueProcessor()
        {
            for (; ; )
            {
                if (_modifyQueryQueue.Count > 0)
                {
                    var args = _modifyQueryQueue.Dequeue();

                    try
                    {
                        var table = _db.Tables.Find(x => x.Name == args.Query.table);
                        var tableIdx = _db.Tables.IndexOf(table);

                        if (table == null || tableIdx <= -1)
                        {
                            if (args.OnError != null) args.OnError("Table doesnt exist");
                            return;
                        }

                        if (args.Query.type == ModificationQueryType.Insert)
                        {
                            if (args.Query.data.Count != table.Keys.Count)
                                throw new Exception("Invalid query signature -- incorrect data given to an insert query.");
                            table.Rows.Add(args.Query.data);

                            _db.Tables[tableIdx] = table;
                            _file.SaveDb(_db);
                        }
                        else if (args.Query.type == ModificationQueryType.Delete)
                        {
                            List<int> indexes = new List<int>();
                            foreach (var key in table.Keys)
                                if (args.Query.keys.Contains(key))
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
                            Console.WriteLine("INFO: running predicate for  " + JsonConvert.SerializeObject(table.Rows[y]));
#endif
                                    if (indexes.Contains(x) && args.Query.predicate(table.Rows[y]))
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
                        else if (args.Query.type == ModificationQueryType.Change)
                        {
                            List<(int keyIdx, int pendingChangeIdx)> indexes = new();
                            foreach (var key in table.Keys)
                                if (args.Query.keys.Contains(key))
                                {
                                    var keyIdx = table.Keys.IndexOf(key);
                                    var pendingChangeIdx = args.Query.keys.IndexOf(key);

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
                            Console.WriteLine("INFO: running predicate for  " + JsonConvert.SerializeObject(table.Rows[y]));
#endif
                                    if (indexes.Any(o => o.keyIdx == x) && args.Query.predicate(table.Rows[y]))
                                    {
                                        var idxInfo = indexes.Find(o => o.keyIdx == x);

#if LOG_MESSAGES
                                Console.WriteLine("INFO: pred passed; query.data.Count = " + args.Query.data.Count + "; idxInfo.pendingChangeIdx = " + idxInfo.pendingChangeIdx);
#endif
                                        pendingChanges.Add(new Tuple<int, int, object>(x, y, args.Query.data[idxInfo.pendingChangeIdx]));
                                    }
                                }

#if LOG_MESSAGES
                    Console.WriteLine("INFO: finished initial table evaluation");
#endif

                            foreach (var change in pendingChanges)
                            {
#if LOG_MESSAGES
                        Console.WriteLine("INFO: changing " + JsonConvert.SerializeObject(table.Rows[change.Item2][change.Item1]) + " --> " + JsonConvert.SerializeObject(change.Item3));
#endif
                                table.Rows[change.Item2][change.Item1] = change.Item3;
                            }

                            _db.Tables[tableIdx] = table;
                            _file.SaveDb(_db);
                        }

                        if (args.OnSuccess != null) args.OnSuccess();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        if (args.OnError != null) args.OnError(ex.Message);
                    }
                    finally
                    {
                        _file.SaveDb(_db);
                    }
                }

                await Task.Delay(25);
            }
        }

        public async Task GetQuery_QueueProcessor()
        {
            for (; ; )
            {
                if (_getQueryQueue.Count > 0)
                {
                    var args = _getQueryQueue.Dequeue();


#if LOG_MESSAGES
                    Console.WriteLine("INFO: getting data from the " + args.TableName + " table");
#endif
                    try
                    {
                        bool foundTable = false;
                        for (int i = 0; i < _db.Tables.Count; i++)
                        {
                            var table = _db.Tables[i];
#if LOG_MESSAGES
                            Console.WriteLine("INFO: found " + table.Name + " table");
#endif
                            List<int> indexes = new List<int>();
                            foreach (var key in table.Keys)
                                if (args.Keys.Contains(key))
                                {
                                    var keyIdx = table.Keys.IndexOf(key);
#if LOG_MESSAGES
                                    Console.WriteLine("WARNING: adding " + key + " to key list; keyIdx = " + keyIdx);
#endif
                                    indexes.Add(table.Keys.IndexOf(key));
                                }

                            if (table.Name == args.TableName)
                            {
#if LOG_MESSAGES
                                Console.WriteLine("INFO: found " + table.Name + " table that was requested");
#endif
                                foreach (var row in table.Rows)
                                {
                                    var rowIdx = table.Rows.IndexOf(row);
                                    var requestedColumns = GetIncludedColumns(indexes, row);
                                    if (args.Predicate(requestedColumns))
                                    {
#if LOG_MESSAGES
                                        Console.WriteLine("INFO: predicate success. took " + rowIdx + " row(s) to find target");
#endif
                                        foundTable = true;
                                        if (args.OnSuccess != null) args.OnSuccess(requestedColumns);
                                        break;
                                    }
                                }
                            }
                        }

                        if (!foundTable)
                        {
#if LOG_MESSAGES
                            Console.WriteLine("WARNING: table " + args.TableName + " was not found in db");
#endif

                            if (args.OnError != null) args.OnError("No table found, or the table data query predicate failed.");
                        }
                    }
                    catch (Exception ex)
                    {
#if LOG_MESSAGES
                        Console.WriteLine("WARNING: " + ex.ToString());
#endif

                        if (args.OnError != null) args.OnError(ex.Message);
                    }
                }

                await Task.Delay(25);
            }
        }

        public async Task GetAllQuery_QueueProcessor()
        {
            for (; ; )
            {
                if (_getAllQueryQueue.Count > 0)
                {
                    var args = _getAllQueryQueue.Dequeue();

                    var data = new List<List<object>>();
                    bool foundTable = false;
#if LOG_MESSAGES
                    Console.WriteLine("INFO: getting data from the " + args.TableName + " table");
#endif
                    for (int i = 0; i < _db.Tables.Count; i++)
                    {
                        var table = _db.Tables[i];
                        List<int> indexes = new List<int>();
                        foreach (var key in table.Keys)
                            if (args.Keys.Contains(key))
                                indexes.Add(table.Keys.IndexOf(key));

                        if (table.Name == args.TableName)
                        {
                            foundTable = true;
                            foreach (var row in table.Rows)
                            {
                                var requestedColumns = GetIncludedColumns(indexes, row);
                                data.Add(requestedColumns);
                            }
                        }
                    }

                    if (!foundTable)
                    {
#if LOG_MESSAGES
                        Console.WriteLine("WARNING: cannot find the " + args.TableName + " table");
#endif
                        if (args.OnError != null) args.OnError("No table was found");
                    }
                    else
                    {
#if LOG_MESSAGES
                        Console.WriteLine("INFO: finished get operation in the " + args.TableName + " table");
#endif
                        if (args.OnSuccess != null) args.OnSuccess(data);
                    }
                }

                await Task.Delay(25);
            }
        }
        #endregion

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
        public void InitTable(InitTableArgs args, Action onSuccess = null!, Action<string> onError = null!)
        {
            if (args.OnSuccess != null) args.OnSuccess = onSuccess;
            if (args.OnError != null) args.OnError = onError;

            _initTableQueue.Enqueue(args);
        }

        /// <summary>
        /// Executes a modification query, and then saves the file.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        public void ModifyQuery(ModifyQueryArgs args, Action onSuccess = null!, Action<string> onError = null!)
        {
            if (args.OnSuccess != null) args.OnSuccess = onSuccess;
            if (args.OnError != null) args.OnError = onError;

            _modifyQueryQueue.Enqueue(args);
        }

        private List<object> GetIncludedColumns(List<int> requestedIndexes, List<object> allValues)
        {
            List<object> result = new List<object>();
            for (int i = 0; i < requestedIndexes.Count; i++)
                for (int j = 0; j < allValues.Count; j++)
                    if (requestedIndexes.Contains(j))
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
        public void GetQuery(GetQueryArgs args, Action<List<object>> onSuccess = null!, Action<string> onError = null!)
        {
            if (args.OnSuccess != null) args.OnSuccess = onSuccess;
            if (args.OnError != null) args.OnError = onError;

            _getQueryQueue.Enqueue(args);
        }

        /// <summary>
        /// Requests all data from a row.
        /// </summary>
        public void GetAllQuery(GetAllQueryArgs args, Action<List<List<object>>> onSuccess = null, Action<string> onError = null)
        {
            if (args.OnSuccess != null) args.OnSuccess = onSuccess;
            if (args.OnError != null) args.OnError = onError;

            this._getAllQueryQueue.Enqueue(args);
        }


        /// <inheritdoc/>
        public void Close(JsonDbClientSession session) 
        {
            _allSessions.Remove(session);
        }

        private bool SessionMatches(JsonDbClientSession a, string b) => a.SessionId == b;

        private JsonDbClientSession GetOrCreateSession(string sessionId)
        {
            if (_allSessions.Any(x => SessionMatches(x, sessionId)))
                return _allSessions.Find(x => SessionMatches(x, sessionId))!;

            var session = new JsonDbClientSession(sessionId, this);
            _allSessions.Add(session);

            return session;
        }

        private JsonDbFile _file;
        private JsonDatabase _db;

        private bool _isAsync;

        private Queue<InitTableArgs> _initTableQueue = new Queue<InitTableArgs>();
        private Queue<ModifyQueryArgs> _modifyQueryQueue = new Queue<ModifyQueryArgs>();
        private Queue<GetQueryArgs> _getQueryQueue = new Queue<GetQueryArgs>();
        private Queue<GetAllQueryArgs> _getAllQueryQueue = new Queue<GetAllQueryArgs>();

        private List<JsonDbClientSession> _allSessions = new List<JsonDbClientSession>();
    }
}
