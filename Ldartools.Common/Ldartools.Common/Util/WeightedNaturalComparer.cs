using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ldartools.Common.Util
{
    public class WeightedNaturalComparer : IComparer<string>
    {
        private readonly bool _ignoreCase;
        private readonly bool _ignoreWhitespace;
        private readonly List<string> _weights;
        private readonly WeightedNaturalComparerOptions _options;

        [Flags]
        public enum WeightedNaturalComparerOptions
        {
            None = 0x00000,
            IgnoreCase = 0x00001,
            IgnoreWhiteSpace = 0x00010,
            StartsWith = 0x00100,
            Contains = 0x01000,
            EndsWith = 0x10000
        }

        public WeightedNaturalComparer(IEnumerable<string> weights, WeightedNaturalComparerOptions options = WeightedNaturalComparerOptions.StartsWith)
        {
            _ignoreCase = options.HasFlag(WeightedNaturalComparerOptions.IgnoreCase);
            _ignoreWhitespace = options.HasFlag(WeightedNaturalComparerOptions.IgnoreWhiteSpace);
            _options = options;
            _weights = _ignoreCase ? weights.Select(w => w.ToLowerInvariant()).ToList() : weights.ToList();
        }

        //less than zero = x is less than y
        //zero = x equals y
        //greater than zero = x is greater than y

        /// <summary>
        /// Compares two strings using a natural sort. Nulls and empty strings are treated as equal.
        /// </summary>
        /// <param name="left">The left string to compare.</param>
        /// <param name="right">The right string to compare.</param>
        /// <returns>If the right string is less than the left the return value will be less than zero.
        ///  If the left string is greater than the right than a value greater than zero is returned.
        ///  If the left string is equal to the right string zero is returned.</returns>
        public int Compare(string left, string right)
        {
            //treat null and empty strings the same, also ignore leading and trailing whitespace
            var x = left?.Trim() ?? string.Empty;
            var y = right?.Trim() ?? string.Empty;

            if (_ignoreCase)
            {
                x = x.ToLower();
                y = y.ToLower();
            }

            if (_ignoreWhitespace)
            {
                var regex = new Regex(@"\s");
                x = regex.Replace(x, string.Empty);
                y = regex.Replace(y, string.Empty);
            }

            var weightedAction = GetWeightedAction();
            // ReSharper disable ImplicitlyCapturedClosure
            var xweightedMatchIndex = _weights.FindIndex(w => weightedAction(x, w));
            var yweightedMatchIndex = _weights.FindIndex(w => weightedAction(y, w));
            // ReSharper restore ImplicitlyCapturedClosure


            if (xweightedMatchIndex != -1 && yweightedMatchIndex == -1)
            {
                return -1;
            }

            if (xweightedMatchIndex == -1 && yweightedMatchIndex != -1)
            {
                return 1;
            }

            //smaller index = higher priority i.e. reverse return values
            if (xweightedMatchIndex > yweightedMatchIndex)
            {
                return 1;
            }

            if (xweightedMatchIndex < yweightedMatchIndex)
            {
                return -1;
            }

            //we can't use iterator since we need to see the next value
            var xarray = x.ToCharArray();
            var yarray = y.ToCharArray();
            int xindex = -1, yindex = -1;
            while (true)
            {
                var xhas = xindex + 1 < xarray.Length;
                var yhas = yindex + 1 < yarray.Length;

                //neither string has another to compare so they are equal
                if (!(xhas || yhas)) return 0;

                //if one string is shorter than the other than the shorter comes first
                if (!xhas) return -1;
                if (!yhas) return 1;

                xindex++;
                yindex++;

                var xchar = xarray[xindex];
                var ychar = yarray[yindex];

                var xIsNum = char.IsDigit(xchar);
                var yIsNum = char.IsDigit(ychar);

                //if both characters are numeric then we have to compare the full numeric string part
                if (xIsNum && yIsNum)
                {
                    var xints = GetNumericString(xarray, ref xindex);
                    var yints = GetNumericString(yarray, ref yindex);
                    var xint = int.Parse(xints);
                    var yint = int.Parse(yints);
                    var iresult = xint.CompareTo(yint);
                    if (iresult != 0) return iresult;
                    continue;
                }

                //if one char is numeric but the other is not then the numeric comes first
                if (xIsNum) return -1;
                if (yIsNum) return 1;

                //both characters are not numeric so we simply compare
                var cresult = xchar.CompareTo(ychar);
                if (cresult != 0) return cresult;
            }

        }

        private Func<string, string, bool> GetWeightedAction()
        {
            if (_options.HasFlag(WeightedNaturalComparerOptions.StartsWith))
            {
                return (source, match) => source.StartsWith(match);
            }
            if (_options.HasFlag(WeightedNaturalComparerOptions.Contains))
            {
                return (source, match) => source.Contains(match);
            }
            if (_options.HasFlag(WeightedNaturalComparerOptions.EndsWith))
            {
                return (source, match) => source.EndsWith(match);
            }
            return (source, match) => source.StartsWith(match);
        }

        private string GetNumericString(IReadOnlyList<char> source, ref int index)
        {
            var sb = new StringBuilder();
            while (index < source.Count && char.IsDigit(source[index]))
            {
                sb.Append(source[index]);
                index++;
            }
            index--;
            return sb.ToString();
        }

    }
}
