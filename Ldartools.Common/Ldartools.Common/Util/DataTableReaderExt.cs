using System;
using System.Collections.Generic;
using System.Data;
using Ldartools.Common.Util.Comparers;

namespace Ldartools.Common.Util
{
    public class DataTableReaderExt : IDataReader, IMappedDataRecord
    {
        private readonly DataTableReader _reader;
        private readonly Dictionary<string, int> _map;

        public DataTableReaderExt(DataTable table, bool caseSensitive = true)
        {
            _reader = new DataTableReader(table);
            _map = caseSensitive ? new Dictionary<string, int>() : new Dictionary<string, int>(new CaseInsensitiveComparer());
            for (var col = 0; col < table.Columns.Count; col++)
            {
                _map.Add(table.Columns[col].ColumnName, col);
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        public bool GetBoolean(int i)
        {
            return _reader.GetBoolean(i);
        }

        public byte GetByte(int i)
        {
            return _reader.GetByte(i);
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            return _reader.GetBytes(i, fieldOffset, buffer, bufferOffset, length);
        }

        public char GetChar(int i)
        {
            return _reader.GetChar(i);
        }

        public long GetChars(int i, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            return _reader.GetChars(i, fieldOffset, buffer, bufferOffset, length);
        }

        public IDataReader GetData(int i)
        {
            return _reader.GetData(i);
        }

        public string GetDataTypeName(int i)
        {
            return _reader.GetDataTypeName(i);
        }

        public DateTime GetDateTime(int i)
        {
            return _reader.GetDateTime(i);
        }

        public decimal GetDecimal(int i)
        {
            return _reader.GetDecimal(i);
        }

        public double GetDouble(int i)
        {
            return _reader.GetDouble(i);
        }

        public Type GetFieldType(int i)
        {
            return _reader.GetFieldType(i);
        }

        public float GetFloat(int i)
        {
            return _reader.GetFloat(i);
        }

        public Guid GetGuid(int i)
        {
            return _reader.GetGuid(i);
        }

        public short GetInt16(int i)
        {
            return _reader.GetInt16(i);
        }

        public int GetInt32(int i)
        {
            return _reader.GetInt32(i);
        }

        public long GetInt64(int i)
        {
            return _reader.GetInt64(i);
        }

        public string GetName(int i)
        {
            return _reader.GetName(i);
        }

        public int GetOrdinal(string name)
        {
            return _map[name];
        }

        public string GetString(int i)
        {
            return _reader.GetString(i);
        }

        public object GetValue(int i)
        {
            return _reader.GetValue(i);
        }

        public int GetValues(object[] values)
        {
            return _reader.GetValues(values);
        }

        public bool IsDBNull(int i)
        {
            return _reader.IsDBNull(i);
        }

        public int FieldCount => _reader.FieldCount;

        public object this[int i] => _reader[i];

        public object this[string name] => _reader[name];

        public void Close()
        {
            _reader.Close();
        }

        public DataTable GetSchemaTable()
        {
            return _reader.GetSchemaTable();
        }

        public bool NextResult()
        {
            return _reader.NextResult();
        }

        public bool Read()
        {
            return _reader.Read();
        }

        public int Depth => _reader.Depth;
        public bool IsClosed => _reader.IsClosed;
        public int RecordsAffected => _reader.RecordsAffected;


        public Type GetFieldType(string name)
        {
            return _reader.GetFieldType(GetOrdinal(name));
        }

        public object GetValue(string name)
        {
            return _reader.GetValue(GetOrdinal(name));
        }

        public bool GetBoolean(string name)
        {
            return _reader.GetBoolean(GetOrdinal(name));
        }

        public byte GetByte(string name)
        {
            return _reader.GetByte(GetOrdinal(name));
        }

        public long GetBytes(string name, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            return _reader.GetBytes(GetOrdinal(name), fieldOffset, buffer, bufferOffset, length);
        }

        public char GetChar(string name)
        {
            return _reader.GetChar(GetOrdinal(name));
        }

        public long GetChars(string name, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            return _reader.GetChars(GetOrdinal(name), fieldOffset, buffer, bufferOffset, length);
        }

        public Guid GetGuid(string name)
        {
            return _reader.GetGuid(GetOrdinal(name));
        }

        public short GetInt16(string name)
        {
            return _reader.GetInt16(GetOrdinal(name));
        }

        public int GetInt32(string name)
        {
            return _reader.GetInt32(GetOrdinal(name));
        }

        public long GetInt64(string name)
        {
            return _reader.GetInt64(GetOrdinal(name));
        }

        public float GetFloat(string name)
        {
            return _reader.GetFloat(GetOrdinal(name));
        }

        public double GetDouble(string name)
        {
            return _reader.GetDouble(GetOrdinal(name));
        }

        public string GetString(string name)
        {
            return _reader.GetString(GetOrdinal(name));
        }

        public decimal GetDecimal(string name)
        {
            return _reader.GetDecimal(GetOrdinal(name));
        }

        public DateTime GetDateTime(string name)
        {
            return _reader.GetDateTime(GetOrdinal(name));
        }

        public IDataReader GetData(string name)
        {
            return _reader.GetData(GetOrdinal(name));
        }

        public bool IsDBNull(string name)
        {
            return _reader.IsDBNull(GetOrdinal(name));
        }


        #region String

        public string GetStringOrDefault(string name)
        {
            return IsDBNull(name) ? default(string) : GetString(name);
        }

        #endregion

        #region Int

        public int GetInt32OrDefault(string name)
        {
            return IsDBNull(name) ? default(int) : GetInt32(name);
        }

        public int? GetNullableInt32(string name)
        {
            return IsDBNull(name) ? new int?() : GetInt32(name);
        }

        #endregion

        #region Long

        public long GetInt64OrDefault(string name)
        {
            return IsDBNull(name) ? default(long) : GetInt64(name);
        }

        public long? GetNullableInt64(string name)
        {
            return IsDBNull(name) ? new long?() : GetInt64(name);
        }

        #endregion

        #region Guid

        public Guid GetGuidOrDefault(string name)
        {
            return IsDBNull(name) ? default(Guid) : GetGuid(name);
        }

        public Guid? GetNullableGuid(string name)
        {
            return IsDBNull(name) ? new Guid?() : GetGuid(name);
        }

        #endregion

        #region DateTime

        public DateTime GetDateTimeOrDefault(string name)
        {
            return IsDBNull(name) ? default(DateTime) : GetDateTime(name);
        }

        public DateTime? GetNullableDateTime(string name)
        {
            return IsDBNull(name) ? new DateTime?() : GetDateTime(name);
        }

        #endregion

        #region Double

        public double GetDoubleOrDefault(string name)
        {
            return IsDBNull(name) ? default(double) : GetDouble(name);
        }

        public double? GetNullableDouble(string name)
        {
            return IsDBNull(name) ? new double?() : GetDouble(name);
        }

        #endregion

        #region Decimal

        public decimal GetDecimalOrDefault(string name)
        {
            return IsDBNull(name) ? default(decimal) : GetDecimal(name);
        }

        public decimal? GetNullableDecimal(string name)
        {
            return IsDBNull(name) ? new decimal?() : GetDecimal(name);
        }

        #endregion

        #region Boolean

        public bool GetBooleanOrDefault(string name)
        {
            return !IsDBNull(name) && GetBoolean(name);
        }

        public bool? GetNullableBoolean(string name)
        {
            return IsDBNull(name) ? new bool?() : GetBoolean(name);
        }

        #endregion

    }
}
