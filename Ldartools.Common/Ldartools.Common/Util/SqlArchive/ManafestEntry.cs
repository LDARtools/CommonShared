using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Ldartools.Common.Util.SqlArchive
{
    public class ManafestEntry
    {
        public string ResultSetName { get; set; }

        public List<ColumnInfo> Schema { get; set; }

        [JsonIgnore]
        public string FilePath { get; set; }

        public SqlFileStream OpenRead()
        {
            return new SqlFileStream(FilePath, SqlFileStreamMode.Read, this);
        }
    }

    [DebuggerDisplay("({Name})[{DataType}]")]
    public class ColumnInfo
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public bool Nullable { get; set; }

        public static IEnumerable<ColumnInfo> FromSchemaTable(DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                yield return new ColumnInfo
                {
                    Name = row["ColumnName"].ToString(),
                    DataType = row["DataType"].ToString(),
                    Nullable = (bool)row["AllowDBNull"]
                };
            }
        }

        public Type LookupDataType()
        {
            var type = Type.GetType(DataType);
            if (Nullable && type != null && type.IsValueType)
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }
            return type;
        }

        public DbType GetDbType()
        {
            switch (DataType)
            {
                case "System.String":
                case "String":
                    return DbType.String;
                case "System.DateTime":
                case "DateTime":
                    return DbType.DateTime2;
                case "System.Int32":
                case "Int":
                case "Integer":
                    return DbType.Int32;
                case "System.Double":
                case "Double":
                    return DbType.Double;
                case "System.Decimal":
                case "Decimal":
                    return DbType.Decimal;
                default:
                    throw new Exception("Unmapped data type: " + DataType);
            }
        }
    }
}
