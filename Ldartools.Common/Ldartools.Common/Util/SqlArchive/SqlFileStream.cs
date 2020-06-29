using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;

namespace Ldartools.Common.Util.SqlArchive
{
    public class SqlFileStream : IDisposable, IDataReader
    {
        private readonly ManafestEntry _entry;
        private readonly SqlFileStreamMode _mode;
        private readonly FileStream _fileStream;
        private readonly GZipStream _compressionStream;
        private readonly StreamReader _streamReader;
        private readonly JsonTextReader _jsonReader;
        private readonly JsonSerializer _jsonSerializer;
        private readonly Dictionary<int, Type> _typeMappings;
        private readonly object[] _objectBuffer;

        internal SqlFileStream(string filename, SqlFileStreamMode mode, ManafestEntry entry) : this(filename, mode)
        {
            _entry = entry;
            if (mode == SqlFileStreamMode.Read)
            {
                var index = 0;
                _typeMappings = entry.Schema.ToDictionary(k => index++, v => v.LookupDataType());
                _objectBuffer = new object[entry.Schema.Count];
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public SqlFileStream(string filename, SqlFileStreamMode mode)
        {
            _mode = mode;
            _jsonSerializer = new JsonSerializer();
            switch (mode)
            {
                case SqlFileStreamMode.Read:
                    _fileStream = File.Open(filename, FileMode.Open);
                    _compressionStream = new GZipStream(_fileStream, CompressionMode.Decompress);
                    _streamReader = new StreamReader(_compressionStream);
                    _jsonReader = new JsonTextReader(_streamReader);
                    break;
                case SqlFileStreamMode.Write:
                    _fileStream = File.Open(filename, FileMode.Create);
                    _compressionStream = new GZipStream(_fileStream, CompressionMode.Compress);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
            IsClosed = false;
        }

        public void Dispose()
        {
            IsClosed = true;
            _jsonReader?.Close();
            _streamReader?.Close();
            _compressionStream.Close();
            _fileStream.Close();
        }

        public void Write(SqlDataReader reader)
        {
            if(_mode != SqlFileStreamMode.Write) throw new Exception("File is not open for write.");
            var schema = reader.GetSchemaTable();

            if (_entry != null)
            {
                _entry.Schema = ColumnInfo.FromSchemaTable(schema).ToList();
            }

            var columnCount = schema.Rows.Count;
            using (var sw = new StreamWriter(_compressionStream))
            using (var writer = new JsonTextWriter(sw))
            {
                writer.WriteStartArray();
                while (reader.Read())
                {
                    var values = new object[columnCount];
                    reader.GetValues(values);
                    _jsonSerializer.Serialize(writer, values);
                }
                writer.WriteEndArray();
            }
        }

        #region Implementation of IDataRecord

        
        public string GetName(int i)
        {
            return _entry.Schema[i].Name;
        }

        
        public string GetDataTypeName(int i)
        {
            return _typeMappings[i].Name;
        }

        
        public Type GetFieldType(int i)
        {
            return _typeMappings[i];
        }

        
        public object GetValue(int i)
        {
            var val = _objectBuffer[i];
            if (_entry.Schema[i].Nullable == false && val == null)
            {
                Debugger.Break();
            }
            return _objectBuffer[i];
        }

        
        public int GetValues(object[] values)
        {
            for (var i = 0; i < _objectBuffer.Length; i++)
            {
                values[i] = _objectBuffer[i];
            }
            return _objectBuffer.Length;
        }

        
        public int GetOrdinal(string name)
        {
            return _entry.Schema.FindIndex(col => col.Name == name);
        }

        
        public bool GetBoolean(int i)
        {
            return (bool)GetValue(i);
        }

        
        public byte GetByte(int i)
        {
            return (byte)GetValue(i);
        }

        
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        
        public char GetChar(int i)
        {
            return (char)GetValue(i);
        }

        
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        
        public Guid GetGuid(int i)
        {
            return (Guid)GetValue(i);
        }

        
        public short GetInt16(int i)
        {
            return (short)GetValue(i);
        }

        
        public int GetInt32(int i)
        {
            return (int)GetValue(i);
        }

        
        public long GetInt64(int i)
        {
            return (long)GetValue(i);
        }

        
        public float GetFloat(int i)
        {
            return (float)GetValue(i);
        }

        
        public double GetDouble(int i)
        {
            return (double)GetValue(i);
        }

        
        public string GetString(int i)
        {
            return (string)GetValue(i);
        }

        
        public decimal GetDecimal(int i)
        {
            return (decimal)GetValue(i);
        }

        
        public DateTime GetDateTime(int i)
        {
            return (DateTime)GetValue(i);
        }

        
        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }

        
        public bool IsDBNull(int i)
        {
            return GetValue(i) == null;
        }

        
        public int FieldCount => _objectBuffer.Length;

        
        object IDataRecord.this[int i] => GetValue(i);

        
        object IDataRecord.this[string name] => GetValue(GetOrdinal(name));

        #endregion

        #region Implementation of IDataReader

        
        public void Close()
        {
            Dispose();
        }

        
        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        
        public bool NextResult()
        {
            throw new NotImplementedException();
        }


        
        public bool Read()
        {
            var index = 0;
            while (_jsonReader.Read())
            {
                if (_jsonReader.Depth > 0)
                {
                    if (_jsonReader.TokenType == JsonToken.StartArray)
                    {
                        index = 0;
                    }
                    else if (_jsonReader.TokenType == JsonToken.EndArray)
                    {
                        //only say we found data if we did
                        return index > 0;
                    }
                    else
                    {
                        _objectBuffer[index] = _jsonSerializer.Deserialize(_jsonReader, _typeMappings[index]);
                        index++;
                    }
                }
            }
            return false;
        }

        
        public int Depth => throw new NotImplementedException();

        
        public bool IsClosed { get; private set; }

        
        public int RecordsAffected => throw new NotImplementedException();

        #endregion
    }
}
