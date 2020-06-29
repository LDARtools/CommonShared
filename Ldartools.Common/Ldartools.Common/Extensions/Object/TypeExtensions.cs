using System;

namespace Ldartools.Common.Extensions.Object
{
    public static class TypeExtensions
    {
        public static Type GetNullableType(this Type type)
        {
            if (type == null || type.IsClass) return type;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return typeof(bool?);
                case TypeCode.Byte:
                    return typeof(byte?);
                case TypeCode.Char:
                    return typeof(char?);
                case TypeCode.DateTime:
                    return typeof(System.DateTime?);
                case TypeCode.Decimal:
                    return typeof(decimal?);
                case TypeCode.Double:
                    return typeof(double?);
                case TypeCode.Int16:
                    return typeof(short?);
                case TypeCode.Int32:
                    return typeof(int?);
                case TypeCode.Int64:
                    return typeof(long?);
                case TypeCode.SByte:
                    return typeof(sbyte?);
                case TypeCode.Single:
                    return typeof(float?);
                case TypeCode.UInt16:
                    return typeof(ushort?);
                case TypeCode.UInt32:
                    return typeof(uint?);
                case TypeCode.UInt64:
                    return typeof(ulong?);
                default:
                    return type;
            }
        }

        public static Type GetNonNullableType(this Type type)
        {
            return type?.IsGenericType == true && type.GetGenericTypeDefinition() == typeof(Nullable<>) ? type.GenericTypeArguments[0] : type;
        }

        public static object GetDefaultValue(this Type t)
        {
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
        
        public static T? ToNullable<T>(this T input) where T : struct
        {
            return Equals(input, default(T)) ? (T?)null : input;
        }
    }
}
