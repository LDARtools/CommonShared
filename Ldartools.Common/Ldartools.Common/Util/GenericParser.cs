using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ldartools.Common.Util
{
    //coppied from https://github.com/chrisg32/Commons/blob/master/Commons/Util/GenericParser.cs
    //one day we will switch to the nuget after our project supports .NetStandard
    public static class GenericParser
    {
        private static readonly Dictionary<Type, Func<string, object>> Parsers = new Dictionary<Type, Func<string, object>>
        {
            { typeof(object), s => s },
            { typeof(string), s => s },
            { typeof(bool), s => bool.Parse(s) },
            { typeof(int), s => int.Parse(s) },
            { typeof(long), s => long.Parse(s) },
            { typeof(double), s => double.Parse(s) },
            { typeof(float), s => float.Parse(s) },
            { typeof(decimal), s => decimal.Parse(s) },
            { typeof(short), s => short.Parse(s) },
            { typeof(byte), s => byte.Parse(s) },
            { typeof(sbyte), s => sbyte.Parse(s) },
            { typeof(uint), s => uint.Parse(s) },
            { typeof(ulong), s => ulong.Parse(s) },
            { typeof(ushort), s => ushort.Parse(s) },
            { typeof(char), s => Convert.ToChar(s) },
            { typeof(bool?), s => string.IsNullOrWhiteSpace(s) ? null : (bool?) bool.Parse(s) },
            { typeof(int?), s => string.IsNullOrWhiteSpace(s) ? null : (int?)  int.Parse(s) },
            { typeof(long?), s => string.IsNullOrWhiteSpace(s) ? null : (long?)  long.Parse(s) },
            { typeof(double?), s => string.IsNullOrWhiteSpace(s) ? null : (double?)  double.Parse(s) },
            { typeof(float?), s => string.IsNullOrWhiteSpace(s) ? null : (float?)  float.Parse(s) },
            { typeof(decimal?), s => string.IsNullOrWhiteSpace(s) ? null : (decimal?)  decimal.Parse(s) },
            { typeof(short?), s => string.IsNullOrWhiteSpace(s) ? null : (short?)  short.Parse(s) },
            { typeof(byte?), s => string.IsNullOrWhiteSpace(s) ? null : (byte?)  byte.Parse(s) },
            { typeof(sbyte?), s => string.IsNullOrWhiteSpace(s) ? null : (sbyte?)  sbyte.Parse(s) },
            { typeof(uint?), s => string.IsNullOrWhiteSpace(s) ? null : (uint?)  uint.Parse(s) },
            { typeof(ulong?), s => string.IsNullOrWhiteSpace(s) ? null : (ulong?)  ulong.Parse(s) },
            { typeof(ushort?), s => string.IsNullOrWhiteSpace(s) ? null : (ushort?)  ushort.Parse(s) },
            { typeof(char?), s => s == " " ? ' ' : (string.IsNullOrWhiteSpace(s) ? null : (char?)  Convert.ToChar(s)) },
            { typeof(DateTime), s => DateTime.Parse(s, CultureInfo.InvariantCulture)},
            { typeof(DateTimeOffset), s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture) },
            { typeof(TimeSpan), s => TimeSpan.Parse(s, CultureInfo.InvariantCulture) },
            { typeof(Guid), s => Guid.Parse(s) },
            { typeof(DateTime?), s => string.IsNullOrWhiteSpace(s) ? null : (DateTime?) DateTime.Parse(s, CultureInfo.InvariantCulture) },
            { typeof(DateTimeOffset?), s => string.IsNullOrWhiteSpace(s) ? null : (DateTimeOffset?) DateTimeOffset.Parse(s, CultureInfo.InvariantCulture) },
            { typeof(TimeSpan?), s => string.IsNullOrWhiteSpace(s) ? null : (TimeSpan?) TimeSpan.Parse(s, CultureInfo.InvariantCulture) },
            { typeof(Guid?), s => string.IsNullOrWhiteSpace(s) ? null : (Guid?) Guid.Parse(s) },
        };

        /// <summary>
        /// Note: DateTime and DateTimeOffset values used Invarient Culture to parse and as such are only percise to the second.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static T Parse<T>(string stringValue)
        {
            var type = typeof(T);
            if (!Parsers.ContainsKey(type)) throw new ArgumentException($"No parser have been registered for type: {type}");
            return (T)Parsers[type].Invoke(stringValue);
        }

        public static bool TryParse<T>(string stringValue, out T value)
        {
            try
            {
                value = Parse<T>(stringValue);
            }
            catch
            {
                value = default(T);
                return false;
            }
            return true;
        }

        public static object Parse(string stringValue, Type type)
        {
            if (!Parsers.ContainsKey(type)) throw new ArgumentException($"No parser have been registered for type: {type}");
            return Parsers[type].Invoke(stringValue);
        }

        public static bool TryParse(string stringValue, out object value, Type type)
        {
            try
            {
                value = Parse(stringValue, type);
            }
            catch
            {
                value = default(object);
                return false;
            }
            return true;
        }

        public static void RegisterParser(Type type, Func<string, object> parseFunc)
        {
            Parsers[type] = parseFunc;
        }
    }
}
