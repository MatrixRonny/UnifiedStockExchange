using ServiceStack.OrmLite;
using System.Data;

Console.Write("SourceDatabase=");
string? srcDb = Console.ReadLine();

Console.Write("DestinationDatabase=");
string? destDb = Console.ReadLine();

IDbConnection srcConn = new OrmLiteConnectionFactory(srcDb, SqliteDialect.Provider).CreateDbConnection();
IDbConnection destConn = new OrmLiteConnectionFactory(destDb, SqliteDialect.Provider).CreateDbConnection();

List<string> tables = srcConn.Select<string>("SELECT name FROM sqlite_master where type='table'");

foreach(string table in tables)
{
    if(!destConn.TableExists(table))
    {

    }
}
