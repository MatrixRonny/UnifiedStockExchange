using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Logging;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Converters;
using System.Data;
using System.Collections;
using ServiceStack.Text;

namespace UnifiedStockExchange.OrmLiteInternals
{
    public static class OrmLiteWriteCommandExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrmLiteWriteCommandExtensions));

        public static bool CreateTable(this IDbCommand dbCmd, bool overwrite, Type modelType)
        {
            var modelDef = modelType.GetModelDefinition();
            return dbCmd.CreateTable(overwrite, modelDef);
        }

        public static bool CreateTable(this IDbCommand dbCmd, bool overwrite, ModelDefinition modelDef)
        {
            var dialectProvider = dbCmd.GetDialectProvider();
            var tableName = dialectProvider.NamingStrategy.GetTableName(modelDef);
            var schema = dialectProvider.NamingStrategy.GetSchemaName(modelDef);
            var tableExists = dialectProvider.DoesTableExist(dbCmd, tableName, schema);
            if (overwrite && tableExists)
            {
                if (modelDef.PreDropTableSql != null)
                {
                    dbCmd.ExecuteSql(modelDef.PreDropTableSql);
                }

                DropTable(dbCmd, modelDef);

                var postDropTableSql = dialectProvider.ToPostDropTableStatement(modelDef);
                if (postDropTableSql != null)
                {
                    dbCmd.ExecuteSql(postDropTableSql);
                }

                if (modelDef.PostDropTableSql != null)
                {
                    dbCmd.ExecuteSql(modelDef.PostDropTableSql);
                }

                tableExists = false;
            }

            try
            {
                if (!tableExists)
                {
                    if (modelDef.PreCreateTableSql != null)
                    {
                        dbCmd.ExecuteSql(modelDef.PreCreateTableSql);
                    }

                    // sequences must be created before tables
                    var sequenceList = dialectProvider.SequenceList(modelDef.ModelType);
                    if (sequenceList.Count > 0)
                    {
                        foreach (var seq in sequenceList)
                        {
                            if (dialectProvider.DoesSequenceExist(dbCmd, seq) == false)
                            {
                                var seqSql = dialectProvider.ToCreateSequenceStatement(modelDef.ModelType, seq);
                                dbCmd.ExecuteSql(seqSql);
                            }
                        }
                    }
                    else
                    {
                        var sequences = dialectProvider.ToCreateSequenceStatements(modelDef.ModelType);
                        foreach (var seq in sequences)
                        {
                            try
                            {
                                dbCmd.ExecuteSql(seq);
                            }
                            catch (Exception ex)
                            {
                                if (IgnoreAlreadyExistsGeneratorError(ex))
                                {
                                    Log.DebugFormat("Ignoring existing generator '{0}': {1}", seq, ex.Message);
                                    continue;
                                }
                                throw;
                            }
                        }
                    }

                    dbCmd.ExecuteSql(dialectProvider.ToCreateTableStatement(modelDef.ModelType));

                    var postCreateTableSql = dialectProvider.ToPostCreateTableStatement(modelDef);
                    if (postCreateTableSql != null)
                    {
                        dbCmd.ExecuteSql(postCreateTableSql);
                    }

                    if (modelDef.PostCreateTableSql != null)
                    {
                        dbCmd.ExecuteSql(modelDef.PostCreateTableSql);
                    }

                    var sqlIndexes = dialectProvider.ToCreateIndexStatements(modelDef.ModelType);
                    foreach (var sqlIndex in sqlIndexes)
                    {
                        try
                        {
                            dbCmd.ExecuteSql(sqlIndex);
                        }
                        catch (Exception exIndex)
                        {
                            if (IgnoreAlreadyExistsError(exIndex))
                            {
                                Log.DebugFormat("Ignoring existing index '{0}': {1}", sqlIndex, exIndex.Message);
                                continue;
                            }
                            throw;
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (IgnoreAlreadyExistsError(ex))
                {
                    Log.DebugFormat("Ignoring existing table '{0}': {1}", modelDef.ModelName, ex.Message);
                    return false;
                }
                throw;
            }
            return false;
        }

        public static void DropTable<T>(this IDbCommand dbCmd)
        {
            DropTable(dbCmd, ModelDefinition<T>.Definition);
        }

        public static void DropTable(this IDbCommand dbCmd, Type modelType)
        {
            DropTable(dbCmd, modelType.GetModelDefinition());
        }

        public static void DropTables(this IDbCommand dbCmd, params Type[] tableTypes)
        {
            foreach (var modelDef in tableTypes.Select(type => type.GetModelDefinition()))
            {
                DropTable(dbCmd, modelDef);
            }
        }

        private static void DropTable(IDbCommand dbCmd, ModelDefinition modelDef)
        {
            try
            {
                var dialectProvider = dbCmd.GetDialectProvider();
                var tableName = dialectProvider.NamingStrategy.GetTableName(modelDef);
                var schema = dialectProvider.NamingStrategy.GetSchemaName(modelDef);

                if (dialectProvider.DoesTableExist(dbCmd, tableName, schema))
                {
                    if (modelDef.PreDropTableSql != null)
                    {
                        dbCmd.ExecuteSql(modelDef.PreDropTableSql);
                    }

                    var dropTableFks = dialectProvider.GetDropForeignKeyConstraints(modelDef);
                    if (!string.IsNullOrEmpty(dropTableFks))
                    {
                        dbCmd.ExecuteSql(dropTableFks);
                    }
                    dbCmd.ExecuteSql("DROP TABLE " + dialectProvider.GetQuotedTableName(modelDef));

                    if (modelDef.PostDropTableSql != null)
                    {
                        dbCmd.ExecuteSql(modelDef.PostDropTableSql);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.DebugFormat("Could not drop table '{0}': {1}", modelDef.ModelName, ex.Message);
                throw;
            }
        }

        public static string LastSql(this IDbCommand dbCmd)
        {
            return dbCmd.CommandText;
        }

        public static int ExecuteSql(this IDbCommand dbCmd, string sql, IEnumerable<IDbDataParameter> sqlParams = null)
        {
            dbCmd.CommandText = sql;

            dbCmd.SetParameters(sqlParams);

            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.ExecuteSql(dbCmd);

            return dbCmd.ExecuteNonQuery();
        }

        public static int ExecuteSql(this IDbCommand dbCmd, string sql, object anonType)
        {
            if (anonType != null)
                dbCmd.SetParameters(anonType.ToObjectDictionary(), excludeDefaults: false, sql: ref sql);

            dbCmd.CommandText = sql;

            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.ExecuteSql(dbCmd);

            return dbCmd.ExecuteNonQuery();
        }

        private static bool IgnoreAlreadyExistsError(Exception ex)
        {
            //ignore Sqlite table already exists error
            const string sqliteAlreadyExistsError = "already exists";
            const string sqlServerAlreadyExistsError = "There is already an object named";
            return ex.Message.Contains(sqliteAlreadyExistsError)
                  || ex.Message.Contains(sqlServerAlreadyExistsError);
        }

        private static bool IgnoreAlreadyExistsGeneratorError(Exception ex)
        {
            const string fbError = "attempt to store duplicate value";
            const string fbAlreadyExistsError = "already exists";
            return ex.Message.Contains(fbError) || ex.Message.Contains(fbAlreadyExistsError);
        }

        public static T PopulateWithSqlReader<T>(this T objWithProperties, IOrmLiteDialectProvider dialectProvider, IDataReader reader)
        {
            var indexCache = reader.GetIndexFieldsCache(ModelDefinition<T>.Definition, dialectProvider);
            var values = new object[reader.FieldCount];
            return objWithProperties.PopulateWithSqlReader(dialectProvider, reader, indexCache, values);
        }

        public static int GetColumnIndex(this IDataReader reader, IOrmLiteDialectProvider dialectProvider, string fieldName)
        {
            try
            {
                return reader.GetOrdinal(dialectProvider.NamingStrategy.GetColumnName(fieldName));
            }
            catch (IndexOutOfRangeException /*ignoreNotFoundExInSomeProviders*/)
            {
                return NotFound;
            }
        }

        private const int NotFound = -1;
        public static T PopulateWithSqlReader<T>(this T objWithProperties,
            IOrmLiteDialectProvider dialectProvider, IDataReader reader,
            Tuple<FieldDefinition, int, IOrmLiteConverter>[] indexCache, object[] values)
        {
            values = reader.PopulateValues(values, dialectProvider);

            foreach (var fieldCache in indexCache)
            {
                try
                {
                    var fieldDef = fieldCache.Item1;
                    var index = fieldCache.Item2;
                    var converter = fieldCache.Item3;

                    if (values != null && values[index] == DBNull.Value)
                    {
                        var value = fieldDef.IsNullable ? null : fieldDef.FieldTypeDefaultValue;
                        if (OrmLiteConfig.OnDbNullFilter != null)
                        {
                            var useValue = OrmLiteConfig.OnDbNullFilter(fieldDef);
                            if (useValue != null)
                                value = useValue;
                        }

                        fieldDef.SetValueFn(objWithProperties, value);
                    }
                    else
                    {
                        var value = converter.GetValue(reader, index, values);
                        if (value == null)
                        {
                            if (!fieldDef.IsNullable)
                                value = fieldDef.FieldTypeDefaultValue;
                            if (OrmLiteConfig.OnDbNullFilter != null)
                            {
                                var useValue = OrmLiteConfig.OnDbNullFilter(fieldDef);
                                if (useValue != null)
                                    value = useValue;
                            }
                            fieldDef.SetValueFn(objWithProperties, value);
                        }
                        else
                        {
                            var fieldValue = converter.FromDbValue(fieldDef.FieldType, value);
                            fieldDef.SetValueFn(objWithProperties, fieldValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    OrmLiteUtils.HandleException(ex);
                }
            }

            return objWithProperties;
        }

        public static object[] PopulateValues(this IDataReader reader, object[] values, IOrmLiteDialectProvider dialectProvider)
        {
            if (!OrmLiteConfig.DeoptimizeReader)
            {
                if (values == null)
                    values = new object[reader.FieldCount];

                try
                {
                    dialectProvider.GetValues(reader, values);
                }
                catch (Exception ex)
                {
                    values = null;
                    Log.Warn("Error trying to use GetValues() from DataReader. Falling back to individual field reads...", ex);
                }
            }
            else
            {
                //Calling GetValues() on System.Data.SQLite.Core ADO.NET Provider changes behavior of reader.GetGuid()
                //So allow providers to by-pass reader.GetValues() optimization.
                values = null;
            }
            return values;
        }

        public static int Update<T>(this IDbCommand dbCmd, T obj, Action<IDbCommand> commandFilter = null)
        {
            OrmLiteConfig.UpdateFilter?.Invoke(dbCmd, obj);

            var dialectProvider = dbCmd.GetDialectProvider();
            var hadRowVersion = dialectProvider.PrepareParameterizedUpdateStatement<T>(dbCmd);
            if (string.IsNullOrEmpty(dbCmd.CommandText))
                return 0;

            dialectProvider.SetParameterValues<T>(dbCmd, obj);

            commandFilter?.Invoke(dbCmd);
            var rowsUpdated = dbCmd.ExecNonQuery();

            if (hadRowVersion && rowsUpdated == 0)
                throw new OptimisticConcurrencyException();

            return rowsUpdated;
        }

        public static int Update<T>(this IDbCommand dbCmd, T[] objs, Action<IDbCommand> commandFilter = null)
        {
            return dbCmd.UpdateAll(objs: objs, commandFilter: commandFilter);
        }

        public static int UpdateAll<T>(this IDbCommand dbCmd, IEnumerable<T> objs, Action<IDbCommand> commandFilter = null)
        {
            IDbTransaction dbTrans = null;

            int count = 0;
            try
            {
                if (dbCmd.Transaction == null)
                    dbCmd.Transaction = dbTrans = dbCmd.Connection.BeginTransaction();

                var dialectProvider = dbCmd.GetDialectProvider();

                var hadRowVersion = dialectProvider.PrepareParameterizedUpdateStatement<T>(dbCmd);
                if (string.IsNullOrEmpty(dbCmd.CommandText))
                    return 0;

                foreach (var obj in objs)
                {
                    OrmLiteConfig.UpdateFilter?.Invoke(dbCmd, obj);

                    dialectProvider.SetParameterValues<T>(dbCmd, obj);

                    commandFilter?.Invoke(dbCmd);
                    commandFilter = null;
                    var rowsUpdated = dbCmd.ExecNonQuery();
                    if (hadRowVersion && rowsUpdated == 0)
                        throw new OptimisticConcurrencyException();

                    count += rowsUpdated;
                }

                dbTrans?.Commit();
            }
            finally
            {
                dbTrans?.Dispose();
            }

            return count;
        }

        private static int AssertRowsUpdated(IDbCommand dbCmd, bool hadRowVersion)
        {
            var rowsUpdated = dbCmd.ExecNonQuery();
            if (hadRowVersion && rowsUpdated == 0)
                throw new OptimisticConcurrencyException();

            return rowsUpdated;
        }

        public static int Delete<T>(this IDbCommand dbCmd, T anonType)
        {
            return dbCmd.Delete<T>((object)anonType);
        }

        public static int Delete<T>(this IDbCommand dbCmd, object anonType)
        {
            var dialectProvider = dbCmd.GetDialectProvider();

            var hadRowVersion = dialectProvider.PrepareParameterizedDeleteStatement<T>(
                dbCmd, anonType.AllFieldsMap<T>());

            dialectProvider.SetParameterValues<T>(dbCmd, anonType);

            return AssertRowsUpdated(dbCmd, hadRowVersion);
        }

        public static int DeleteNonDefaults<T>(this IDbCommand dbCmd, T filter)
        {
            var dialectProvider = dbCmd.GetDialectProvider();
            var hadRowVersion = dialectProvider.PrepareParameterizedDeleteStatement<T>(
                dbCmd, filter.AllFieldsMap<T>().NonDefaultsOnly());

            dialectProvider.SetParameterValues<T>(dbCmd, filter);

            return AssertRowsUpdated(dbCmd, hadRowVersion);
        }

        public static int Delete<T>(this IDbCommand dbCmd, T[] objs)
        {
            if (objs.Length == 0)
                return 0;

            return dbCmd.DeleteAll(objs);
        }

        public static int DeleteNonDefaults<T>(this IDbCommand dbCmd, T[] filters)
        {
            if (filters.Length == 0) return 0;

            return DeleteAll(dbCmd, filters, o => o.AllFieldsMap<T>().NonDefaultsOnly());
        }

        private static int DeleteAll<T>(IDbCommand dbCmd, IEnumerable<T> objs, Func<object, Dictionary<string, object>> fieldValuesFn = null)
        {
            IDbTransaction dbTrans = null;

            int count = 0;
            try
            {
                if (dbCmd.Transaction == null)
                    dbCmd.Transaction = dbTrans = dbCmd.Connection.BeginTransaction();

                var dialectProvider = dbCmd.GetDialectProvider();

                foreach (var obj in objs)
                {
                    var fieldValues = fieldValuesFn != null
                        ? fieldValuesFn(obj)
                        : obj.AllFieldsMap<T>();

                    dialectProvider.PrepareParameterizedDeleteStatement<T>(dbCmd, fieldValues);

                    dialectProvider.SetParameterValues<T>(dbCmd, obj);

                    count += dbCmd.ExecNonQuery();
                }

                dbTrans?.Commit();
            }
            finally
            {
                dbTrans?.Dispose();
            }

            return count;
        }

        public static int DeleteById<T>(this IDbCommand dbCmd, object id)
        {
            var sql = dbCmd.DeleteByIdSql<T>(id);

            return dbCmd.ExecuteSql(sql);
        }

        public static string DeleteByIdSql<T>(this IDbCommand dbCmd, object id)
        {
            var modelDef = ModelDefinition<T>.Definition;
            var dialectProvider = dbCmd.GetDialectProvider();
            var idParamString = dialectProvider.GetParam();

            var sql = $"DELETE FROM {dialectProvider.GetQuotedTableName(modelDef)} " +
                      $"WHERE {dialectProvider.GetQuotedColumnName(modelDef.PrimaryKey.FieldName)} = {idParamString}";

            var idParam = dbCmd.CreateParameter();
            idParam.ParameterName = idParamString;
            idParam.Value = id;
            dbCmd.Parameters.Add(idParam);
            return sql;
        }

        public static void DeleteById<T>(this IDbCommand dbCmd, object id, ulong rowVersion)
        {
            var sql = dbCmd.DeleteByIdSql<T>(id, rowVersion);

            var rowsAffected = dbCmd.ExecuteSql(sql);
            if (rowsAffected == 0)
                throw new OptimisticConcurrencyException("The row was modified or deleted since the last read");
        }

        public static string DeleteByIdSql<T>(this IDbCommand dbCmd, object id, ulong rowVersion)
        {
            var modelDef = ModelDefinition<T>.Definition;
            var dialectProvider = dbCmd.GetDialectProvider();

            dbCmd.Parameters.Clear();

            var idParam = dbCmd.CreateParameter();
            idParam.ParameterName = dialectProvider.GetParam();
            idParam.Value = id;
            dbCmd.Parameters.Add(idParam);

            var rowVersionField = modelDef.RowVersion;
            if (rowVersionField == null)
                throw new InvalidOperationException(
                    "Cannot use DeleteById with rowVersion for model type without a row version column");

            var rowVersionParam = dbCmd.CreateParameter();
            rowVersionParam.ParameterName = dialectProvider.GetParam("rowVersion");
            var converter = dialectProvider.GetConverterBestMatch(typeof(RowVersionConverter));
            converter.InitDbParam(rowVersionParam, typeof(ulong));

            rowVersionParam.Value = converter.ToDbValue(typeof(ulong), rowVersion);
            dbCmd.Parameters.Add(rowVersionParam);

            var sql = $"DELETE FROM {dialectProvider.GetQuotedTableName(modelDef)} " +
                      $"WHERE {dialectProvider.GetQuotedColumnName(modelDef.PrimaryKey.FieldName)} = {idParam.ParameterName} " +
                      $"AND {dialectProvider.GetRowVersionColumn(rowVersionField)} = {rowVersionParam.ParameterName}";

            return sql;
        }

        public static int DeleteByIds<T>(this IDbCommand dbCmd, IEnumerable idValues)
        {
            var sqlIn = dbCmd.SetIdsInSqlParams(idValues);
            if (string.IsNullOrEmpty(sqlIn))
                return 0;

            var sql = GetDeleteByIdsSql<T>(sqlIn, dbCmd.GetDialectProvider());

            return dbCmd.ExecuteSql(sql);
        }

        public static string GetDeleteByIdsSql<T>(string sqlIn, IOrmLiteDialectProvider dialectProvider)
        {
            var modelDef = ModelDefinition<T>.Definition;

            var sql = $"DELETE FROM {dialectProvider.GetQuotedTableName(modelDef)} " +
                      $"WHERE {dialectProvider.GetQuotedColumnName(modelDef.PrimaryKey.FieldName)} IN ({sqlIn})";
            return sql;
        }

        public static int DeleteAll<T>(this IDbCommand dbCmd)
        {
            return dbCmd.DeleteAll(typeof(T));
        }

        public static int DeleteAll<T>(this IDbCommand dbCmd, IEnumerable<T> rows)
        {
            var ids = rows.Map(x => x.GetId());
            return dbCmd.DeleteByIds<T>(ids);
        }

        public static int DeleteAll(this IDbCommand dbCmd, Type tableType)
        {
            return dbCmd.ExecuteSql(dbCmd.GetDialectProvider().ToDeleteStatement(tableType, null));
        }

        public static int Delete<T>(this IDbCommand dbCmd, string sql, object anonType = null)
        {
            if (anonType != null) dbCmd.SetParameters<T>(anonType, excludeDefaults: false, sql: ref sql);
            return dbCmd.ExecuteSql(dbCmd.GetDialectProvider().ToDeleteStatement(typeof(T), sql));
        }

        public static int Delete(this IDbCommand dbCmd, Type tableType, string sql, object anonType = null)
        {
            if (anonType != null) dbCmd.SetParameters(tableType, anonType, excludeDefaults: false, sql: ref sql);
            return dbCmd.ExecuteSql(dbCmd.GetDialectProvider().ToDeleteStatement(tableType, sql));
        }

        public static long Insert<T>(this IDbCommand dbCmd, T obj, Action<IDbCommand> commandFilter, bool selectIdentity = false)
        {
            OrmLiteConfig.InsertFilter?.Invoke(dbCmd, obj);

            var dialectProvider = dbCmd.GetDialectProvider();
            dialectProvider.PrepareParameterizedInsertStatement<T>(dbCmd,
                insertFields: dialectProvider.GetNonDefaultValueInsertFields(obj));

            dialectProvider.SetParameterValues<T>(dbCmd, obj);

            commandFilter?.Invoke(dbCmd); //dbCmd.OnConflictInsert() needs to be applied before last insert id

            if (dialectProvider.HasInsertReturnValues(ModelDefinition<T>.Definition))
            {
                using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
                using (reader)
                {
                    return reader.PopulateReturnValues(dialectProvider, obj);
                }
            }

            if (selectIdentity)
            {
                dbCmd.CommandText += dialectProvider.GetLastInsertIdSqlSuffix<T>();

                return dbCmd.ExecLongScalar();
            }

            return dbCmd.ExecNonQuery();
        }

        public static long PopulateReturnValues<T>(this IDataReader reader, IOrmLiteDialectProvider dialectProvider, T obj)
        {
            if (reader.Read())
            {
                var modelDef = ModelDefinition<T>.Definition;
                var values = new object[reader.FieldCount];
                var indexCache = reader.GetIndexFieldsCache(modelDef, dialectProvider);
                obj.PopulateWithSqlReader(dialectProvider, reader, indexCache, values);
                if (modelDef.PrimaryKey != null && modelDef.PrimaryKey.AutoIncrement)
                {
                    var id = modelDef.GetPrimaryKey(obj);
                    return Convert.ToInt64(id);
                }
            }

            return 0;
        }

        public static void Insert<T>(this IDbCommand dbCmd, Action<IDbCommand> commandFilter, params T[] objs)
        {
            dbCmd.InsertAll(objs: objs, commandFilter: commandFilter);
        }

        public static long InsertIntoSelect<T>(this IDbCommand dbCmd, ISqlExpression query, Action<IDbCommand> commandFilter) =>
            dbCmd.InsertIntoSelectInternal<T>(query, commandFilter).ExecNonQuery();

        public static IDbCommand InsertIntoSelectInternal<T>(this IDbCommand dbCmd, ISqlExpression query, Action<IDbCommand> commandFilter)
        {
            var dialectProvider = dbCmd.GetDialectProvider();

            var sql = query.ToSelectStatement();
            var selectFields = query.GetUntypedSqlExpression()
                .SelectExpression
                .Substring("SELECT ".Length)
                .ParseCommands();

            var fieldsOrAliases = selectFields
                .Map(x => x.Original.ToString().LastRightPart(" AS ").Trim().StripQuotes());

            dialectProvider.PrepareParameterizedInsertStatement<T>(dbCmd, insertFields: fieldsOrAliases);

            dbCmd.SetParameters(query.Params);

            dbCmd.CommandText = dbCmd.CommandText.LeftPart(")") + ")\n" + sql;

            commandFilter?.Invoke(dbCmd); //dbCmd.OnConflictInsert() needs to be applied before last insert id
            return dbCmd;
        }

        public static void InsertAll<T>(this IDbCommand dbCmd, IEnumerable<T> objs, Action<IDbCommand> commandFilter)
        {
            IDbTransaction dbTrans = null;

            try
            {
                if (dbCmd.Transaction == null)
                    dbCmd.Transaction = dbTrans = dbCmd.Connection.BeginTransaction();

                var dialectProvider = dbCmd.GetDialectProvider();

                dialectProvider.PrepareParameterizedInsertStatement<T>(dbCmd);

                commandFilter?.Invoke(dbCmd);

                foreach (var obj in objs)
                {
                    OrmLiteConfig.InsertFilter?.Invoke(dbCmd, obj);
                    dialectProvider.SetParameterValues<T>(dbCmd, obj);

                    try
                    {
                        dbCmd.ExecNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"SQL ERROR: {dbCmd.GetLastSqlAndParams()}", ex);
                        throw;
                    }
                }

                dbTrans?.Commit();
            }
            finally
            {
                dbTrans?.Dispose();
            }
        }

        public static void InsertUsingDefaults<T>(this IDbCommand dbCmd, params T[] objs)
        {
            IDbTransaction dbTrans = null;

            try
            {
                if (dbCmd.Transaction == null)
                    dbCmd.Transaction = dbTrans = dbCmd.Connection.BeginTransaction();

                var dialectProvider = dbCmd.GetDialectProvider();

                var modelDef = typeof(T).GetModelDefinition();
                var fieldsWithoutDefaults = modelDef.FieldDefinitionsArray
                    .Where(x => x.DefaultValue == null)
                    .Select(x => x.Name)
                    .ToSet();

                dialectProvider.PrepareParameterizedInsertStatement<T>(dbCmd,
                    insertFields: fieldsWithoutDefaults);

                foreach (var obj in objs)
                {
                    OrmLiteConfig.InsertFilter?.Invoke(dbCmd, obj);
                    dialectProvider.SetParameterValues<T>(dbCmd, obj);

                    try
                    {
                        dbCmd.ExecNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"SQL ERROR: {dbCmd.GetLastSqlAndParams()}", ex);
                        throw;
                    }
                }

                dbTrans?.Commit();
            }
            finally
            {
                dbTrans?.Dispose();
            }
        }

        public static int Save<T>(this IDbCommand dbCmd, params T[] objs)
        {
            return dbCmd.SaveAll(objs);
        }

        public static bool Save<T>(this IDbCommand dbCmd, T obj)
        {
            var modelDef = typeof(T).GetModelDefinition();
            var id = modelDef.GetPrimaryKey(obj);
            var existingRow = id != null ? dbCmd.SingleById<T>(id) : default;

            if (Equals(existingRow, default(T)))
            {
                if (modelDef.HasAutoIncrementId)
                {
                    var dialectProvider = dbCmd.GetDialectProvider();
                    var newId = dbCmd.Insert(obj, commandFilter: null, selectIdentity: true);
                    var safeId = dialectProvider.FromDbValue(newId, modelDef.PrimaryKey.FieldType);
                    modelDef.PrimaryKey.SetValueFn(obj, safeId);
                    id = newId;
                }
                else
                {
                    dbCmd.Insert(obj, commandFilter: null);
                }

                modelDef.RowVersion?.SetValueFn(obj, dbCmd.GetRowVersion(modelDef, id, modelDef.RowVersion.ColumnType));

                return true;
            }

            dbCmd.Update(obj);

            modelDef.RowVersion?.SetValueFn(obj, dbCmd.GetRowVersion(modelDef, id, modelDef.RowVersion.ColumnType));

            return false;
        }

        public static int SaveAll<T>(this IDbCommand dbCmd, IEnumerable<T> objs)
        {
            var saveRows = objs.ToList();

            var firstRow = saveRows.FirstOrDefault();
            if (Equals(firstRow, default(T))) return 0;

            var modelDef = typeof(T).GetModelDefinition();

            var firstRowId = modelDef.GetPrimaryKey(firstRow);
            var defaultIdValue = firstRowId?.GetType().GetDefaultValue();

            var idMap = defaultIdValue != null
                ? saveRows.Where(x => !defaultIdValue.Equals(modelDef.GetPrimaryKey(x))).ToSafeDictionary(x => modelDef.GetPrimaryKey(x))
                : saveRows.Where(x => modelDef.GetPrimaryKey(x) != null).ToSafeDictionary(x => modelDef.GetPrimaryKey(x));

            var existingRowsMap = dbCmd.SelectByIds<T>(idMap.Keys).ToDictionary(x => modelDef.GetPrimaryKey(x));

            var rowsAdded = 0;

            IDbTransaction dbTrans = null;

            if (dbCmd.Transaction == null)
                dbCmd.Transaction = dbTrans = dbCmd.Connection.BeginTransaction();

            try
            {
                foreach (var row in saveRows)
                {
                    var id = modelDef.GetPrimaryKey(row);
                    if (id != defaultIdValue && existingRowsMap.ContainsKey(id))
                    {
                        OrmLiteConfig.UpdateFilter?.Invoke(dbCmd, row);
                        dbCmd.Update(row);
                    }
                    else
                    {
                        if (modelDef.HasAutoIncrementId)
                        {
                            var dialectProvider = dbCmd.GetDialectProvider();
                            var newId = dbCmd.Insert(row, commandFilter: null, selectIdentity: true);
                            var safeId = dialectProvider.FromDbValue(newId, modelDef.PrimaryKey.FieldType);
                            modelDef.PrimaryKey.SetValueFn(row, safeId);
                            id = newId;
                        }
                        else
                        {
                            OrmLiteConfig.InsertFilter?.Invoke(dbCmd, row);
                            dbCmd.Insert(row, commandFilter: null);
                        }

                        rowsAdded++;
                    }

                    modelDef.RowVersion?.SetValueFn(row, dbCmd.GetRowVersion(modelDef, id, modelDef.RowVersion.ColumnType));
                }

                dbTrans?.Commit();
            }
            finally
            {
                dbTrans?.Dispose();
                if (dbCmd.Transaction == dbTrans)
                    dbCmd.Transaction = null;
            }

            return rowsAdded;
        }

        public static void SaveAllReferences<T>(this IDbCommand dbCmd, T instance)
        {
            var modelDef = ModelDefinition<T>.Definition;
            var pkValue = modelDef.PrimaryKey.GetValue(instance);

            var fieldDefs = modelDef.AllFieldDefinitionsArray.Where(x => x.IsReference);

            bool updateInstance = false;
            foreach (var fieldDef in fieldDefs)
            {
                var listInterface = fieldDef.FieldType.GetTypeWithGenericInterfaceOf(typeof(IList<>));
                if (listInterface != null)
                {
                    var refType = listInterface.GetGenericArguments()[0];
                    var refModelDef = refType.GetModelDefinition();

                    var refField = modelDef.GetRefFieldDef(refModelDef, refType);

                    var results = (IEnumerable)fieldDef.GetValue(instance);
                    if (results != null)
                    {
                        foreach (var oRef in results)
                        {
                            refField.SetValueFn(oRef, pkValue);
                        }

                        dbCmd.CreateTypedApi(refType).SaveAll(results);
                    }
                }
                else
                {
                    var refType = fieldDef.FieldType;
                    var refModelDef = refType.GetModelDefinition();

                    var refSelf = modelDef.GetSelfRefFieldDefIfExists(refModelDef, fieldDef);

                    var result = fieldDef.GetValue(instance);
                    var refField = refSelf == null
                        ? modelDef.GetRefFieldDef(refModelDef, refType)
                        : modelDef.GetRefFieldDefIfExists(refModelDef);

                    if (result != null)
                    {
                        if (refField != null && refSelf == null)
                            refField.SetValueFn(result, pkValue);

                        dbCmd.CreateTypedApi(refType).Save(result);

                        //Save Self Table.RefTableId PK
                        if (refSelf != null)
                        {
                            var refPkValue = refModelDef.PrimaryKey.GetValue(result);
                            refSelf.SetValueFn(instance, refPkValue);
                            updateInstance = true;
                        }
                    }
                }
            }

            if (updateInstance)
            {
                dbCmd.Update(instance);
            }
        }

        public static void SaveReferences<T, TRef>(this IDbCommand dbCmd, T instance, params TRef[] refs)
        {
            var modelDef = ModelDefinition<T>.Definition;
            var pkValue = modelDef.PrimaryKey.GetValue(instance);

            var refType = typeof(TRef);
            var refModelDef = ModelDefinition<TRef>.Definition;

            var refSelf = modelDef.GetSelfRefFieldDefIfExists(refModelDef, null);

            foreach (var oRef in refs)
            {
                var refField = refSelf == null
                    ? modelDef.GetRefFieldDef(refModelDef, refType)
                    : modelDef.GetRefFieldDefIfExists(refModelDef);

                refField?.SetValueFn(oRef, pkValue);
            }

            dbCmd.SaveAll(refs);

            foreach (var oRef in refs)
            {
                //Save Self Table.RefTableId PK
                if (refSelf != null)
                {
                    var refPkValue = refModelDef.PrimaryKey.GetValue(oRef);
                    refSelf.SetValueFn(instance, refPkValue);
                    dbCmd.Update(instance);
                }
            }
        }

        // Procedures
        public static void ExecuteProcedure<T>(this IDbCommand dbCmd, T obj)
        {
            var dialectProvider = dbCmd.GetDialectProvider();
            dialectProvider.PrepareStoredProcedureStatement(dbCmd, obj);
            dbCmd.ExecuteNonQuery();
        }

        public static object GetRowVersion(this IDbCommand dbCmd, ModelDefinition modelDef, object id, Type asType)
        {
            var sql = dbCmd.RowVersionSql(modelDef, id);
            var to = dbCmd.GetDialectProvider().FromDbRowVersion(modelDef.RowVersion.FieldType, dbCmd.Scalar<object>(sql));

            if (to is ulong u && asType == typeof(byte[]))
                return BitConverter.GetBytes(u);

            return to;
        }

        public static string RowVersionSql(this IDbCommand dbCmd, ModelDefinition modelDef, object id)
        {
            var dialectProvider = dbCmd.GetDialectProvider();
            var idParamString = dialectProvider.GetParam();

            var sql = $"SELECT {dialectProvider.GetRowVersionSelectColumn(modelDef.RowVersion)} " +
                      $"FROM {dialectProvider.GetQuotedTableName(modelDef)} " +
                      $"WHERE {dialectProvider.GetQuotedColumnName(modelDef.PrimaryKey.FieldName)} = {idParamString}";

            dbCmd.Parameters.Clear();
            var idParam = dbCmd.CreateParameter();
            idParam.Direction = ParameterDirection.Input;
            idParam.ParameterName = idParamString;

            dialectProvider.SetParamValue(idParam, id, modelDef.PrimaryKey.ColumnType, modelDef.PrimaryKey);

            dbCmd.Parameters.Add(idParam);
            return sql;
        }
    }
}
