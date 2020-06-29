using System;
using System.Data;

namespace Ldartools.Common.Util
{
    public interface IMappedDataRecord : IDataRecord
    {
        Type GetFieldType(string name);

        object GetValue(string name);

        bool GetBoolean(string name);

        byte GetByte(string name);

        long GetBytes(string name, long fieldOffset, byte[] buffer, int bufferOffset, int length);

        char GetChar(string name);

        long GetChars(string name, long fieldOffset, char[] buffer, int bufferOffset, int length);

        Guid GetGuid(string name);

        short GetInt16(string name);

        int GetInt32(string name);

        long GetInt64(string name);

        float GetFloat(string name);

        double GetDouble(string name);

        string GetString(string name);

        Decimal GetDecimal(string name);

        DateTime GetDateTime(string name);

        IDataReader GetData(string name);

        bool IsDBNull(string name);
    }
}
