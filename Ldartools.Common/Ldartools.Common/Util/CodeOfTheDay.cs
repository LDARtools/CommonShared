using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Util
{
    public class CodeOfTheDay
    {
        public static string GetCodeOfTheDay(DateTime today)
        {
            var dateStr = today.Day.ToString("D2") + today.Month.ToString("D2") + today.Year.ToString();
            var dateStrInt = int.Parse(dateStr);
            var dateMult42 = dateStrInt * 0.042;
            var dateMult42Hex = ((int)dateMult42).ToString("X");
            var fiveZero = new string(dateMult42Hex.ToCharArray().Reverse().Take(4).ToArray());

            return fiveZero;
        }

        public static bool DoesCodeMatch(string code, DateTime today)
        {
            if (GetCodeOfTheDay(today) == code) return true;

            return false;
        }
    }
}
