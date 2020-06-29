using System;
using System.Globalization;

namespace Ldartools.Common.Extensions.DateTime
{
    public static class DateUtil
    {
        public const string DateTimeFormat = @"MM/dd/yyyy hh:mm:ss tt";
        public const string DateFormat = @"MM/dd/yyyy";

        public static string ToDateTimeString(this System.DateTime dateTime)
        {
            return dateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }

        public static string ToDateString(this System.DateTime dateTime)
        {
            return dateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        private static readonly string[] Formats = {
            DateTimeFormat,
            DateFormat,
            @"MM/dd/yyyy HH:mm:ss"
        };

        public static bool TryParse(string stringVal, out System.DateTime dateTime, string tryThisFormatFirst = null)
        {
            if (string.IsNullOrWhiteSpace(stringVal))
            {
                dateTime = default(System.DateTime);
                return false;
            }
            if (!string.IsNullOrWhiteSpace(tryThisFormatFirst))
            {
                if (System.DateTime.TryParseExact(stringVal, tryThisFormatFirst, CultureInfo.CurrentUICulture.DateTimeFormat, DateTimeStyles.None, out dateTime))
                {
                    return true;
                }
            }

            if (System.DateTime.TryParseExact(stringVal, Formats, CultureInfo.CurrentUICulture.DateTimeFormat, DateTimeStyles.None, out dateTime))
            {
                return true;
            }

            return System.DateTime.TryParse(stringVal, out dateTime);
        }

        public static System.DateTime Parse(string stringVal, string tryThisFormatFirst = null)
        {
            if (TryParse(stringVal, out var dateTime, tryThisFormatFirst))
            {
                return dateTime;
            }
            throw new FormatException($"The string '{stringVal}' could not be parsed into a DateTime.");
        }
    }
}
