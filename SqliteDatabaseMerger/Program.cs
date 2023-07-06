using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

IOrmLiteDialectProvider dialectProvider = SqliteDialect.Provider;

Console.Write("SourceDatabase=");
string? srcDb = Console.ReadLine()?.Trim('"');

Console.Write("DestinationDatabase=");
string? destDb = Console.ReadLine()?.Trim('"');

IDbConnection srcConn = new OrmLiteConnectionFactory(srcDb, dialectProvider).CreateDbConnection();
IDbConnection destConn = new OrmLiteConnectionFactory(destDb, dialectProvider).CreateDbConnection();
srcConn.Open();
destConn.Open();

//DEBUG: Trying to see if native library is considerably faster.
//IDbConnection srcConn2 = new SQLiteConnection("Data Source=" + srcDb);
//IDbConnection destConn2 = new SQLiteConnection("Data Source=" + destDb);
//srcConn2.Open();
//destConn2.Open();

List<string> tableNames = srcConn.Select<string>("SELECT name FROM sqlite_schema WHERE type='table'");

//TODO: Need to consider FK graph when inserting. Also, create transaction to ensure join data is consistent after insert.
for(int index=0; index<tableNames.Count; index++)
{
    // Preserve previous final print.
    Console.WriteLine();

    string tableName = tableNames[index];
    if(!destConn.TableExists(tableName))
    {
        string tableSchema = srcConn.Single<string>("SELECT sql FROM sqlite_schema WHERE type='table' AND name=@tableName", new { tableName});
        destConn.ExecuteNonQuery(tableSchema);

        List<string> indexQueries = srcConn.Select<string>("SELECT sql FROM sqlite_schema WHERE type='index' AND tbl_name=@tableName", new { tableName });
        foreach(string query in indexQueries.Where(it => it != null))
        {
            destConn.ExecuteNonQuery(query);
        }
    }

    string selectCount = $"SELECT COUNT(*) FROM {dialectProvider.GetQuotedTableName(tableName)}";
    string selectAll = $"SELECT * FROM {dialectProvider.GetQuotedTableName(tableName)}";
    using (IDataReader reader = srcConn.ExecuteReader($"{selectCount}; {selectAll}"))
    {
        reader.Read();
        int totalRecords = reader.GetInt32(0);
        reader.NextResult();

        IDbCommand insertCommand = destConn.CreateCommand();
        IList<string> columns = Enumerable.Range(0, reader.FieldCount).Select(it => reader.GetName(it)).ToList();
        insertCommand.CommandText = $"INSERT INTO {dialectProvider.GetQuotedTableName(tableName)}\r\n";
        insertCommand.CommandText += $"VALUES({String.Join(',', columns.Select((it, i) => "@" + i))})";

        for (int i = 0; i < columns.Count; i++)
        {
            DbType dbType = dialectProvider.GetConverterBestMatch(reader.GetFieldType(i)).DbType;
            insertCommand.AddParam(i.ToString(), dbType: dbType);
        }

        int batchCount = 0;
        IDbTransaction? trans = null;
        int duplicateCount = 0;
        int recordNumber = 0;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        while (reader.Read())
        {
            recordNumber++;

            if(batchCount == 0)
            {
                trans = destConn.BeginTransaction();
                insertCommand.Transaction = trans;
                batchCount = 1000;
            }
            for (int i = 0; i < columns.Count; i++)
            {
                ((SQLiteParameter)insertCommand.Parameters[i]!).Value = reader.GetValue(i);
            }

            try
            {
                // Decreasing batchCount in case of error prevents delaying the transaction.
                batchCount--;
                insertCommand.ExecuteNonQuery();
            }
            catch(SQLiteException e) when(e.ErrorCode == 19)
            {
                //INFO: Skip duplicate record.
                duplicateCount++;
            }

            if (batchCount == 0)
            {
                trans!.Commit();
                trans.Dispose();
            }
            
            Console.Write(
                "\rTable {0}/{1}, Record {2}/{3}, Duplicate {4}({5:0.00}%), Speed {6:0.00}/second",
                index + 1, tableNames.Count,
                recordNumber, totalRecords,
                duplicateCount, duplicateCount * 100.0 / recordNumber,
                recordNumber / stopwatch.Elapsed.TotalSeconds
            );
        }

        trans?.Commit();
        trans?.Dispose();

        Console.Write(
            "\rTable {0}/{1}, Record {2}/{3}, Duplicate {4}({5}%)",
            index + 1, tableNames.Count,
            recordNumber, totalRecords,
            duplicateCount, duplicateCount * 100.0 / recordNumber
        );
    }
}

srcConn.Close();
destConn.Close();

Console.WriteLine();
Console.ReadKey();