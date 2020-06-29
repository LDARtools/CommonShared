using System;

namespace Ldartools.Common.Extensions.Numeric
{
    public static class NumericExtensions
    {
        public static decimal? ToDecimal(this double? value)
        {
            if (value == null) return null;
            return Convert.ToDecimal(value.Value);
        }

        public static string ToString(this decimal? value, string format, string nullValue = "")
        {
            return value == null ? nullValue : value.Value.ToString(format);
        }

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static bool DoubleEquals(this double d1, double d2, double tolerance = 0.0001)
        {
            return Math.Abs(d1 - d2) < tolerance;
        }

        public static string ToStringWithOrdinal(this int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }
    }
}