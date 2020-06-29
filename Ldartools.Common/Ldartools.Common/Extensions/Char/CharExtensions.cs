namespace Ldartools.Common.Extensions.Char
{
    /// <summary>
    /// Defines extensions for char objects
    /// </summary>
    public static class CharExtensions
    {
        /// <summary>
        /// Checks if a char is uppercase.
        /// </summary>
        /// <param name="ch">The char to check</param>
        /// <returns>Returns true if the char is uppercase</returns>
        public static bool IsUpper(this char ch)
        {
            return char.IsUpper(ch);
        }

        /// <summary>
        /// Checks if a char is lowercase.
        /// </summary>
        /// <param name="ch">The char to check</param>
        /// <returns>Returns true if the char is lowercase</returns>
        public static bool IsLower(this char ch)
        {
            return char.IsLower(ch);
        }

        /// <summary>
        /// Converts a char to uppercase
        /// </summary>
        /// <param name="ch">The char to convert</param>
        /// <returns>Returns the char uppercased</returns>
        public static char ToUpper(this char ch)
        {
            return char.ToUpper(ch);
        }

        /// <summary>
        /// Converts a char to lowercase
        /// </summary>
        /// <param name="ch">The char to convert</param>
        /// <returns>Returns the char lowercased</returns>
        public static char ToLower(this char ch)
        {
            return char.ToLower(ch);
        }
    }

}
