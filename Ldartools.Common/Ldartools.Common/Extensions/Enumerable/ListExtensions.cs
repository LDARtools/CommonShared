using System;
using System.Collections.Generic;

namespace Ldartools.Common.Extensions.Enumerable
{
    public static class ListExtensions
    {
        public static void RemoveRange<TType>(this IList<TType> list, IEnumerable<TType> range)
        {
            if(range == null) throw new ArgumentNullException(nameof(range));
            foreach (var item in range)
            {
                list.Remove(item);
            }
        }

        public static bool Replace<TType>(this IList<TType> list, TType oldValue, TType newValue)
        {
            var index = list.IndexOf(oldValue);
            if (index < 0) return false;
            list[index] = newValue;
            return true;
        }

        public static IEnumerable<TType> AsCircular<TType>(this IList<TType> list, int index)
        {
            if(index >= list.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index must be within the list.");
            for (var i = index; i < list.Count; i++)
            {
                yield return list[i];
            }
            for (var i = 0; i < index; i++)
            {
                yield return list[i];
            }
        }
    }
}
