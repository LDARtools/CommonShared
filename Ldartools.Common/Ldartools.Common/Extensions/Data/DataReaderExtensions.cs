using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Ldartools.Common.Extensions.Enumerable;
using Ldartools.Common.Util.Comparers;

namespace Ldartools.Common.Extensions.Data
{
    public static class DataReaderExtensions
    {
        #region String

        public static string GetStringOrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(string) : reader.GetString(ordinal);
        }

        #endregion

        #region Int

        public static int GetInt32OrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(int) : reader.GetInt32(ordinal);
        }

        public static int? GetNullableInt32(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new int?() : reader.GetInt32(ordinal);
        }

        #endregion

        #region Long

        public static long GetInt64OrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(long) : reader.GetInt64(ordinal);
        }

        public static long? GetNullableInt64(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new long?() : reader.GetInt64(ordinal);
        }

        #endregion

        #region Guid

        public static Guid GetGuidOrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(Guid) : reader.GetGuid(ordinal);
        }

        public static Guid? GetNullableGuid(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new Guid?() : reader.GetGuid(ordinal);
        }

        #endregion

        #region DateTime

        public static System.DateTime GetDateTimeOrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(System.DateTime) : reader.GetDateTime(ordinal);
        }

        public static System.DateTime? GetNullableDateTime(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new System.DateTime?() : reader.GetDateTime(ordinal);
        }

        #endregion

        #region Double

        public static double GetDoubleOrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(double) : reader.GetDouble(ordinal);
        }

        public static double? GetNullableDouble(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new double?() : reader.GetDouble(ordinal);
        }

        #endregion

        #region Decimal

        public static decimal GetDecimalOrDefault(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? default(decimal) : reader.GetDecimal(ordinal);
        }

        public static decimal? GetNullableDecimal(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new decimal?() : reader.GetDecimal(ordinal);
        }

        #endregion

        #region Boolean

        public static bool GetBooleanOrDefault(this IDataReader reader, int ordinal)
        {
            return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
        }

        public static bool? GetNullableBoolean(this IDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? new bool?() : reader.GetBoolean(ordinal);
        }

        #endregion

        public static TValue GetEnum<TValue>(this SqlDataReader reader, int ordinal)
        {
            return (TValue) Enum.Parse(typeof(TValue), reader.GetString(ordinal));
        }

        public static IEnumerable<TValue> ToObjects<TValue>(this SqlDataReader reader) where TValue : new()
        {
            var mapping = reader.GetOrdinalMapping();
            var meta = typeof(TValue).GetProperties().SelectAtIntersection(mapping, (prop, col) => prop.Name == col.Key,
                (prop, col) => new {Prop = prop, Col = col}).ToList();
            if (meta.Any())
            {
                while (reader.Read())
                {
                    var instance = new TValue();
                    foreach (var propertMeta in meta)
                    {
                        var value = reader.GetValue(propertMeta.Col.Value);
                        if (value != DBNull.Value)
                        {
                            if (value is int intVal && (propertMeta.Prop.PropertyType == typeof(bool) || propertMeta.Prop.PropertyType == typeof(Boolean)))
                            {
                                propertMeta.Prop.SetValue(instance, intVal > 0);
                            }
                            else
                            {
                                propertMeta.Prop.SetValue(instance, value);
                            }
                        }
                    }

                    yield return instance;
                }
            }
        }

        public static Dictionary<string, int> GetOrdinalMapping(this SqlDataReader reader)
        {
            var dict = new Dictionary<string, int>(new CaseInsensitiveComparer());
            var schema = reader.GetSchemaTable();
            if (schema == null) throw new Exception("Cannot get schema table.");
            foreach (DataRow row in schema.Rows)
            {
                if (dict.ContainsKey((string) row[0])) continue;
                dict.Add((string) row[0], (int) row[1]);
            }
            return dict;
        }
    }
}
