using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;
using System.Data;
using System.Data.SQLite;

IOrmLiteDialectProvider dialectProvider = SqliteDialect.Provider;

Console.Write("SourceDatabase=");
//string? srcDb = Console.ReadLine();
string? srcDb = @"C:\Users\MatrixRonny\Desktop\New folder\StockDataDB-new.sqlite";

Console.Write("DestinationDatabase=");
//string? destDb = Console.ReadLine();
string? destDb = @"C:\Users\MatrixRonny\Desktop\New folder\StockDataDB-old2.sqlite";

IDbConnection srcConn = new OrmLiteConnectionFactory(srcDb, dialectProvider).CreateDbConnection();
IDbConnection destConn = new OrmLiteConnectionFactory(destDb, dialectProvider).CreateDbConnection();

srcConn.Open();
destConn.Open();

List<string> tableNames = srcConn.Select<string>("SELECT name FROM sqlite_schema WHERE type='table'");

//TODO: Need to consider FK graph when inserting. Also, create transaction to ensure join data is consistent after insert.
foreach(string table in tableNames)
{
    if(!destConn.TableExists(table))
    {
        string tableSchema = srcConn.Single<string>("SELECT sql FROM sqlite_schema WHERE type='table' AND name=@tableName", new { tableName = table});
        destConn.ExecuteNonQuery(tableSchema);

        List<string> indexQueries = srcConn.Select<string>("SELECT sql FROM sqlite_schema WHERE type='index' AND tbl_name=@tableName", new { tableName = table });
        foreach(string query in indexQueries.Where(it => it != null))
        {
            destConn.ExecuteNonQuery(query);
        }
    }

    using (IDataReader reader = srcConn.ExecuteReader($"SELECT * FROM {dialectProvider.GetQuotedTableName(table)}"))
    {
        IDbCommand insertCommand = destConn.CreateCommand();
        IList<string> columns = Enumerable.Range(0, reader.FieldCount).Select(it => reader.GetName(it)).ToList();
        insertCommand.CommandText = $"INSERT INTO {dialectProvider.GetQuotedTableName(table)}";
        insertCommand.CommandText += $" VALUES({String.Join(',', columns.Select((it, i) => "@" + i))})";

        for (int i = 0; i < columns.Count; i++)
        {
            DbType dbType = dialectProvider.GetConverterBestMatch(reader.GetFieldType(i)).DbType;
            insertCommand.AddParam(i.ToString(), dbType: dbType);
        }

        using (IDbTransaction trans = destConn.BeginTransaction())
        {
            int batchCount = 1000;
            while (reader.Read())
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    ((SQLiteParameter)insertCommand.Parameters[i]!).Value = reader.GetValue(i);
                }

                try
                {
                    insertCommand.ExecuteNonQuery();
                    batchCount--;
                }
                catch
                {
                    //EMPTY: Skip inserting record.
                }

                if (batchCount == 0)
                {
                    trans.Commit();
                    batchCount = 1000;
                }
            }

            trans.Commit();
        }
    }
}

srcConn.Close();
destConn.Close();