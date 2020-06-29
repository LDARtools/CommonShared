using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using Ldartools.Common.Extensions.Enumerable;

namespace Ldartools.Common.Extensions.Data
{
    public static class DataTableExtensions
    {
        public static IEnumerable<string> GetColumnNames(this DataColumnCollection columns)
        {
            foreach (DataColumn column in columns)
            {
                yield return column.ColumnName;
            }
        }

        public static IEnumerable<string> GetColumnNames(this DataTable table)
        {
            return table.Columns.GetColumnNames();
        }

        public static Dictionary<string, int> GetOrdinalMapping(this DataTable table)
        {
            var dict = new Dictionary<string, int>();
            for (var col = 0; col < table.Columns.Count; col++)
            {
                dict.Add(table.Columns[col].ColumnName, col);
            }
            return dict;
        }

        public static DataColumn Add<TType>(this DataColumnCollection columns, string name)
        {
            return columns.Add(name, typeof(TType));
        }

        public static IEnumerable<TValue> ToObjects<TValue>(this DataTable table, CompareOptions columnCompareOptions = CompareOptions.None) where TValue : new()
        {
            bool DefaultMappingFunction(string s1, string s2)
            {
                return string.Compare(s1, s2, CultureInfo.InvariantCulture, columnCompareOptions) == 0;
            }
            return table.ToObjects<TValue>(DefaultMappingFunction);
        }

        public static IEnumerable<TValue> ToObjects<TValue>(this DataTable table, Func<string, string, bool> mappingFunc) where TValue : new()
        {
            var meta = typeof(TValue).GetProperties().SelectAtIntersection(table.GetOrdinalMapping(),
                (prop, col) => mappingFunc(prop.Name, col.Key), (prop, col) => new { Ordinal = col.Value, Prop = prop }).ToList();
            using (var reader = new DataTableReader(table))
            {
                while (reader.Read())
                {
                    var instance = new TValue();
                    foreach (var propertMeta in meta)
                    {
                        var value = reader.GetValue(propertMeta.Ordinal);
                        if (value != DBNull.Value)
                        {
                            propertMeta.Prop.SetValue(instance, value);
                        }
                    }
                    yield return instance;
                }
            }
        }

        /// <summary>
        /// Create a data table compatible with the [dto].[GuidList] user defined sql data type.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static DataTable ToSqlGuidList(this IEnumerable<Guid> source)
        {
            var table = new DataTable("[dto].[GuidList]");
            table.Columns.Add("Value", typeof(Guid));
            source.Distinct().ForEach(id => table.Rows.Add(id));
            return table;
        }

        public static DataTable ExecuteTable(this SqlCommand command)
        {
            var set = new DataSet();
            using (var adapter = new SqlDataAdapter(command))
            {
                adapter.Fill(set);
            }
            return set.Tables[0];
        }
    }
}
