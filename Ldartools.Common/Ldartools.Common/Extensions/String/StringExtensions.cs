using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ldartools.Common.Extensions.Char;
using Ldartools.Common.Extensions.Enumerable;
using Ldartools.Common.Util;

namespace Ldartools.Common.Extensions.String
{

    /// <summary>
    /// Defines extensions for string objects
    /// </summary>
    public static class StringExtensions
    {
        public static string ToTitleCase(this string str)
        {
            var sb = new StringBuilder();
            var prev = default(char);
            foreach (var c in str.Trim())
            {
                if (prev == ' ' || prev == default(char))
                {
                    if (c != ' ')
                    {
                        sb.Append(c.ToUpper());
                    }
                }
                else if (prev.IsLower() && c.IsUpper())
                {
                    sb.Append(' ');
                    sb.Append(c);
                }
                else
                {
                    sb.Append(c);
                }
                prev = c;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Checks if the string matches the given regular expression.
        /// </summary>
        /// <param name="str">The string to match against the regular expression</param>
        /// <param name="pattern">The regular expression</param>
        /// <returns>Returns true if the string matches the given regular expression</returns>
        public static bool IsMatch(this string str, string pattern)
        {
            return Regex.IsMatch(str, pattern);
        }

        /// <summary>
        /// Returns the match from the string given a regular expression.
        /// </summary>
        /// <param name="str">The string to match against the regular expression</param>
        /// <param name="pattern">The regular expression</param>
        /// <returns>Returns the match from the string given a regular expression</returns>
        public static string Match(this string str, string pattern)
        {
            return Regex.Match(str, pattern).Value;
        }

        /// <summary>
        /// Returns a substring starting at startPos to the end of the given string.
        /// Negative numbers are allowed.  (e.g. -1 will get the end character)
        /// </summary>
        /// <param name="str">The string to get the range from</param>
        /// <param name="startPos">The start position of the </param>
        /// <returns>Returns a substring starting at startPos to the end of the given string</returns>
        public static string Range(this string str, int startPos)
        {
            return Range(str, startPos, 0);
        }

        /// <summary>
        /// Returns a substring starting at startPos to the end of the given string.
        /// Negative numbers are allowed (e.g. -1 will get the end character).
        /// The end position character is not included.
        /// </summary>
        /// <param name="str">The string to get the range from</param>
        /// <param name="startPos">The start position</param>
        /// <param name="endPos">The end position</param>
        /// <returns>Returns a substring starting at startPos to the end of the given string</returns>
        public static string Range(this string str, int startPos, int endPos)
        {
            string retStr = System.String.Empty;

            startPos = ConvertToPositiveIndex(startPos, str.Length);
            endPos = ConvertToPositiveIndex(endPos, str.Length);

            if (startPos > endPos)
            {
                retStr += str.Substring(startPos);
                startPos = 0;
            }

            return retStr + str.Substring(startPos, endPos - startPos);
        }

        /// <summary>
        /// Converts the string to be more 'human friendly'.
        /// Returns a string with spaces inserted before uppercase characters.
        /// </summary>
        /// <param name="str">The pascal-cased string to convert</param>
        /// <returns>Returns a string with spaces inserted before uppercase characters</returns>
        public static string Humanize(this string str)
        {
            return str.SplitOn(ch => ch.IsUpper()).ToStringJoin(" ");
        }
        /// <summary>
        /// splits string preserving Capitalization in string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string HumanizeCaps(this string str)
        {
            str = str.Replace(" ", string.Empty);
            string retString = "";
            string part = "";
            int i = str.Length;
            for (int X = 0; X < i; X++)
            {
                if (str.Substring(X, 1) == str.Substring(X, 1).ToUpper())
                {
                    part = "";
                    int Z = X;
                    while (str.Substring(Z, 1) == str.Substring(Z, 1).ToUpper())
                    {
                        part += str.Substring(Z, 1);

                        Z++;

                        if (Z == i)
                            break;
                    }
                    if (Z - X > 1)
                    {
                        if (X != 0)
                            retString += " ";

                        if (Z != i)
                        {
                            retString += str.Substring(X, Z - X - 1);
                            X = Z - 2;
                        }
                        else
                        {
                            retString += str.Substring(X, Z - X);
                            X = Z;
                        }
                    }
                    else
                    {
                        if (X != 0)
                            retString += " ";

                        retString += str.Substring(X, 1);
                    }
                }
                else
                {
                    retString += str.Substring(X, 1);
                }
            }
            return retString;


            //return str.SplitOn(ch => ch.IsUpper()).ToStringJoin(" ");
        }

        /// <summary>
        /// Cleans out white space from a string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string CleanWiteSpace(this string str)
        {
            return str.Replace(" ", string.Empty);
        }

        /// <summary>
        /// Returns an array of strings split by the condition.
        /// </summary>
        /// <param name="str">The string to iterate over</param>
        /// <param name="condition">The condition to evaluate each character against</param>
        /// <returns>Returns an array of strings split by the condition</returns>
        public static string[] SplitOn(this string str, Predicate<char> condition)
        {
            List<string> splitString = new List<string>();
            string strElem = "";

            foreach (char ch in str)
            {
                if (condition(ch))
                {
                    if (!strElem.IsEmpty())
                        splitString.Add(strElem);

                    strElem = "";
                }

                strElem += ch;
            }

            if (!strElem.IsEmpty())
                splitString.Add(strElem);

            return splitString.ToArray();
        }

        /// <summary>
        /// Checks if the string is empty.
        /// </summary>
        /// <param name="str">The string to evaluate</param>
        /// <returns>Returns true if the string is empty</returns>
        public static bool IsEmpty(this string str)
        {
            return str.Length == 0;
        }

        /// <summary>
        /// Checks if the string is empty or null.
        /// </summary>
        /// <param name="str">The string to evaluate</param>
        /// <returns>Returns true if the string is empty or blank</returns>
        public static bool IsBlank(this string str)
        {
            return str == null || str.IsEmpty();
        }

        /// <summary>
        /// Converts a negative index into a positive index relative to the length.
        /// </summary>
        /// <param name="index">The index to convert</param>
        /// <param name="length">The length of the string</param>
        /// <returns>Returns a positive index relative to the length</returns>
        private static int ConvertToPositiveIndex(int index, int length)
        {
            if (index < 0)
                index = length + index;

            return index;
        }

        public static T Parse<T>(this string s)
        {
            return GenericParser.Parse<T>(s);
        }

        public static string ReduceWhitespace(this string s)
        {
            return Regex.Replace(s, @"\s+", " ");
        }

        public static string NextString(this Random random, int minLength = 10, int maxLength = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var length = random.Next(minLength, maxLength);
            return new string(System.Linq.Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }

}
