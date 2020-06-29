using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Ldartools.Common.Extensions.Object
{
    public static class ObjectExtensions
    {

        /// <summary>
        /// Checks if the object is null.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool IsNull(this object obj)
        {
            return obj == null;
        }

        private static readonly Dictionary<Type, SqlDbType> DatabaseTypeMapping = new Dictionary<Type, SqlDbType>
        {
            { typeof(bool), SqlDbType.Bit },
            { typeof(bool?), SqlDbType.Bit },
            { typeof(int), SqlDbType.Int },
            { typeof(int?), SqlDbType.Int },
            { typeof(Guid), SqlDbType.UniqueIdentifier },
            { typeof(Guid?), SqlDbType.UniqueIdentifier },
            { typeof(System.DateTime), SqlDbType.DateTime2 },
            { typeof(System.DateTime?), SqlDbType.DateTime2 },
            { typeof(decimal), SqlDbType.Decimal },
            { typeof(decimal?), SqlDbType.Decimal },
            { typeof(string), SqlDbType.NVarChar },
            { typeof(byte[]), SqlDbType.VarBinary },
            { typeof(DataTable), SqlDbType.Structured }
        };

        public static SqlParameter ToSqlParameter(this object o, string name)
        {
            if (DatabaseTypeMapping.TryGetValue(o.GetType(), out var dbType))
            {
                if (dbType == SqlDbType.Structured)
                {
                    return new SqlParameter(name, dbType) { Value = o ?? DBNull.Value, TypeName = "dbo.GuidList" };
                }

                return new SqlParameter(name, dbType) { Value = o ?? DBNull.Value };
            }

            return new SqlParameter("@" + name, SqlDbType.NVarChar) { Value = o ?? DBNull.Value };
        }

        public static IEnumerable<SqlParameter> ToSqlParameters(this object o, params string[] properties)
        {
            var query = properties != null && properties.Any()
                ? o.GetType().GetProperties().Where(prop => properties.Contains(prop.Name))
                : o.GetType().GetProperties();
            foreach (var prop in query)
            {
                if (DatabaseTypeMapping.TryGetValue(prop.PropertyType, out var dbType))
                {
                    var value = prop.GetValue(o) ?? DBNull.Value;
                    var param = new SqlParameter("@" + prop.Name, dbType) {Value = value};
                    yield return param;
                }
            }
        }

        public static object AsDbNull(this object obj)
        {
            return string.IsNullOrWhiteSpace(obj?.ToString()) ? DBNull.Value : obj;
        }

        public static object AsDbNull(this Guid guid)
        {
            return guid == default(Guid) ? (object) DBNull.Value : guid;
        }

        public static object AsDbNull(this Guid? guid, bool emptyIsNull = true)
        {
            return guid == null || emptyIsNull && guid.Value == default(Guid) ? (object)DBNull.Value : guid;
        }

        public static object AsDbNull(this System.DateTime dateTime)
        {
            return dateTime == default(System.DateTime) ? (object) DBNull.Value : dateTime;
        }

        public static object AsDbNull(this System.DateTime? dateTime, bool defaultIsNull = true)
        {
            return dateTime == null || defaultIsNull && dateTime.Value == default(System.DateTime) ? (object)DBNull.Value : dateTime;
        }

        public static TValue As<TValue>(this object obj) where TValue : class
        {
            return obj as TValue;
        }

        public static TValue Cast<TValue>(this object obj) where TValue : class
        {
            return (TValue)obj;
        }
    }

}
