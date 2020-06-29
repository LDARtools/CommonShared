using System;
using System.Collections;
using Ldartools.Common.Extensions.BitByte;

namespace Ldartools.Common.Extensions.DateTime
{
    public static class DataTimeExtensions
    {
        public static bool PrecisionEquals(this System.DateTime dateTime, System.DateTime other, DateTimeEqualityPrecision precision)
        {
            var bits = new BitArray(new[] {(int) precision}).ToArray(7);
            return (dateTime.Year == other.Year || !bits[0]) &&
                   (dateTime.Month == other.Month || !bits[1]) &&
                   (dateTime.Day == other.Day || !bits[2]) &&
                   (dateTime.Hour == other.Hour || !bits[3]) &&
                   (dateTime.Minute == other.Minute || !bits[4]) &&
                   (dateTime.Second == other.Second || !bits[5]) &&
                   (dateTime.Millisecond == other.Millisecond || !bits[6]);
        }

        public static System.DateTime NextDate(this Random random, System.DateTime? max = null)
        {
            if(max == null) max = System.DateTime.Now;
            var start = new System.DateTime(1990, 1, 1);
            var range = (max.Value - start).Days;
            return start.AddDays(random.Next(range));
        }
    }

    public enum DateTimeEqualityPrecision
    {
        Year = 0x1000000,
        Month= 0x1100000,
        Day = 0x1110000,
        Hour = 0x1111000,
        Min = 0x1111100,
        Second = 0x1111110,
        Millisecond = 0x1111111
    }
}
