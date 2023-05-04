// Copyright (c) ServiceStack, Inc. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt


using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.OrmLite;

namespace UnifiedStockExchange.OrmLiteInternals
{
    public static class OrmLiteResultsFilterExtensions
    {
        public static ILog Log = LogManager.GetLogger(typeof(OrmLiteResultsFilterExtensions));

        public static int ExecNonQuery(this IDbCommand dbCmd, string sql, object anonType = null)
        {
            if (anonType != null)
                dbCmd.SetParameters(anonType.ToObjectDictionary(), false, sql: ref sql);

            dbCmd.CommandText = sql;

            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.ExecuteSql(dbCmd);

            return dbCmd.ExecuteNonQuery();
        }

        public static int ExecNonQuery(this IDbCommand dbCmd, string sql, Dictionary<string, object> dict)
        {

            if (dict != null)
                dbCmd.SetParameters(dict, false, sql: ref sql);

            dbCmd.CommandText = sql;

            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.ExecuteSql(dbCmd);

            return dbCmd.ExecuteNonQuery();
        }

        public static int ExecNonQuery(this IDbCommand dbCmd)
        {
            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.ExecuteSql(dbCmd);

            return dbCmd.ExecuteNonQuery();
        }

        public static int ExecNonQuery(this IDbCommand dbCmd, string sql, Action<IDbCommand> dbCmdFilter)
        {
            dbCmdFilter?.Invoke(dbCmd);

            dbCmd.CommandText = sql;

            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.ExecuteSql(dbCmd);

            return dbCmd.ExecuteNonQuery();
        }

        public static List<T> ConvertToList<T>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            var isScalar = OrmLiteUtils.IsScalar<T>();

            if (OrmLiteConfig.ResultsFilter != null)
            {
                return isScalar
                    ? OrmLiteConfig.ResultsFilter.GetColumn<T>(dbCmd)
                    : OrmLiteConfig.ResultsFilter.GetList<T>(dbCmd);
            }

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return isScalar
                    ? reader.Column<T>(dbCmd.GetDialectProvider())
                    : reader.ConvertToList<T>(dbCmd.GetDialectProvider());
            }
        }

        public static IList ConvertToList(this IDbCommand dbCmd, Type refType, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetRefList(dbCmd, refType);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ConvertToList(dbCmd.GetDialectProvider(), refType);
            }
        }

        public static IDbDataParameter PopulateWith(this IDbDataParameter to, IDbDataParameter from)
        {
            to.ParameterName = from.ParameterName;
            to.DbType = from.DbType;
            to.Value = from.Value;

            if (from.Precision != default(byte))
                to.Precision = from.Precision;
            if (from.Scale != default(byte))
                to.Scale = from.Scale;
            if (from.Size != default)
                to.Size = from.Size;

            return to;
        }

        public static List<T> ExprConvertToList<T>(this IDbCommand dbCmd, string sql = null, IEnumerable<IDbDataParameter> sqlParams = null, HashSet<string> onlyFields = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            dbCmd.SetParameters(sqlParams);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetList<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ConvertToList<T>(dbCmd.GetDialectProvider(), onlyFields: onlyFields);
            }
        }

        public static T ConvertTo<T>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetSingle<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ConvertTo<T>(dbCmd.GetDialectProvider());
            }
        }

        public static object ConvertTo(this IDbCommand dbCmd, Type refType, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetRefSingle(dbCmd, refType);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ConvertTo(dbCmd.GetDialectProvider(), refType);
            }
        }

        public static T Scalar<T>(this IDbCommand dbCmd, string sql, IEnumerable<IDbDataParameter> sqlParams)
        {
            return dbCmd.SetParameters(sqlParams).Scalar<T>(sql);
        }

        public static T Scalar<T>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetScalar<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.Scalar<T>(dbCmd.GetDialectProvider());
            }
        }

        public static object Scalar(this IDbCommand dbCmd, ISqlExpression sqlExpression)
        {
            dbCmd.PopulateWith(sqlExpression);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetScalar(dbCmd);

            return dbCmd.ExecuteScalar();
        }

        public static object Scalar(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetScalar(dbCmd);

            return dbCmd.ExecuteScalar();
        }

        public static long ExecLongScalar(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (Log.IsDebugEnabled)
                Log.DebugCommand(dbCmd);

            OrmLiteConfig.BeforeExecFilter?.Invoke(dbCmd);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetLongScalar(dbCmd);

            return dbCmd.LongScalar();
        }

        public static T ExprConvertTo<T>(this IDbCommand dbCmd, string sql = null, IEnumerable<IDbDataParameter> sqlParams = null, HashSet<string> onlyFields = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            dbCmd.SetParameters(sqlParams);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetSingle<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ConvertTo<T>(dbCmd.GetDialectProvider(), onlyFields: onlyFields);
            }
        }

        public static List<T> Column<T>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetColumn<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.Column<T>(dbCmd.GetDialectProvider());
            }
        }

        public static List<T> Column<T>(this IDbCommand dbCmd, string sql, IEnumerable<IDbDataParameter> sqlParams)
        {
            return dbCmd.SetParameters(sqlParams).Column<T>(sql);
        }

        public static HashSet<T> ColumnDistinct<T>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetColumnDistinct<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ColumnDistinct<T>(dbCmd.GetDialectProvider());
            }
        }

        public static HashSet<T> ColumnDistinct<T>(this IDbCommand dbCmd, ISqlExpression expression)
        {
            dbCmd.PopulateWith(expression);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetColumnDistinct<T>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.ColumnDistinct<T>(dbCmd.GetDialectProvider());
            }
        }

        public static Dictionary<K, V> Dictionary<K, V>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetDictionary<K, V>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.Dictionary<K, V>(dbCmd.GetDialectProvider());
            }
        }

        public static Dictionary<K, V> Dictionary<K, V>(this IDbCommand dbCmd, ISqlExpression expression)
        {
            dbCmd.PopulateWith(expression);

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetDictionary<K, V>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.Dictionary<K, V>(dbCmd.GetDialectProvider());
            }
        }

        public static Dictionary<K, List<V>> Lookup<K, V>(this IDbCommand dbCmd, string sql, IEnumerable<IDbDataParameter> sqlParams)
        {
            return dbCmd.SetParameters(sqlParams).Lookup<K, V>(sql);
        }

        public static Dictionary<K, List<V>> Lookup<K, V>(this IDbCommand dbCmd, string sql = null)
        {
            if (sql != null)
                dbCmd.CommandText = sql;

            if (OrmLiteConfig.ResultsFilter != null)
                return OrmLiteConfig.ResultsFilter.GetLookup<K, V>(dbCmd);

            using (var reader = dbCmd.ExecReader(dbCmd.CommandText))
            {
                return reader.Lookup<K, V>(dbCmd.GetDialectProvider());
            }
        }
    }
}
