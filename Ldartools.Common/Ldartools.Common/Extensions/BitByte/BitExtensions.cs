using System.Collections;
using System.Linq;

namespace Ldartools.Common.Extensions.BitByte
{
    public static class BitExtensions
    {
        public static bool[] ToArray(this BitArray bitArray, int bits, bool reverse = true)
        {
            var array = new bool[bits];
            for (var i = 0; i < bits; i++)
            {
                array[i] = bitArray[i];
            }
            return reverse ? array.Reverse().ToArray() : array;
        }
    }
}
