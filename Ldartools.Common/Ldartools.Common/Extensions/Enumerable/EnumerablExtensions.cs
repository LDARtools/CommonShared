using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Ldartools.Common.Extensions.String;
using Ldartools.Common.Util;

namespace Ldartools.Common.Extensions.Enumerable
{
    /// <summary>
    /// Defines extension methods for Enumerable objects
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Invokes an action for each element in the IEnumerable.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the IEnumerable</typeparam>
        /// <param name="collection">The IEnumberable object to iterate over.</param>
        /// <param name="action">The action to apply to each element</param>
        public static void Each<T>(this IEnumerable<T> collection, Action<T> action)
        {
            if(action == null) throw new ArgumentNullException(nameof(action));
            foreach (T t in collection)
            {
                action(t);
            }
        }

        public static IEnumerable<T> ForEachThen<T>(this IEnumerable<T> collection, Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (var i in collection)
            {
                action(i);
                yield return i;
            }
        }

        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (var i in collection)
            {
                action(i);
            }
        }

        /// <summary>
        /// Invokes an action for each element in the IEnumerable with the index for each element.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the IEnumerable</typeparam>
        /// <param name="collection">The IEnumberable object to iterate over.</param>
        /// <param name="action">The action to apply to each element</param>
        public static void Each<T>(this IEnumerable<T> collection, Action<T, int> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var index = 0;
            foreach (T t in collection)
            {
                action(t, index++);
            }
        }

        /// <summary>
        /// Returns an IEnumerable object without elements that match the given condition.
        /// </summary>
        /// <typeparam name="T">The type of the IEnumerable</typeparam>
        /// <param name="collection">The object to iterate over</param>
        /// <param name="condition">The condition to evaluate</param>
        /// <returns>Returns an IEnumerable object without elements that match the given condition</returns>
        public static IEnumerable<T> DeleteIf<T>(this IEnumerable<T> collection, Predicate<T> condition)
        {
            foreach (T t in collection)
            {
                if (!condition(t))
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Returns a string of the given IEnumerable with each element suffixed with the separator
        /// except for the last.
        /// <example>{1, 2, 3, 4}.ToStringJoin("-") => "1-2-3-4"</example>
        /// </summary>
        /// <typeparam name="T">The type of each element in the IEnumerable</typeparam>
        /// <param name="collection">The object to iterate over</param>
        /// <param name="separator">The string suffix each element with, except for the last</param>
        /// <returns>Returns a string of the given IEnumerable with each element suffixed with the separator
        /// except for the last
        /// </returns>
        public static string ToStringJoin<T>(this IEnumerable<T> collection, string separator)
        {
            var retStr = System.String.Empty;

            foreach (T t in collection)
            {
                retStr += t + separator;
            }

            if (string.IsNullOrEmpty(retStr)) return retStr;

            retStr = separator.IsBlank() ? retStr : retStr.Range(0, -separator.Length);

            return retStr;
        }

        /// <summary>
        /// Returns a string of the given IEnumerable with each element suffixed with the separator
        /// except for the last.
        /// <example>{1, 2, 3, 4}.ToStringJoin() => "1234"</example>
        /// </summary>
        /// <typeparam name="T">The type of each element in the IEnumerable</typeparam>
        /// <param name="collection">The object to iterate over</param>
        /// <returns>Returns a string of the given IEnumerable with each element suffixed with the separator
        /// except for the last
        /// </returns>
        public static string ToStringJoin<T>(this IEnumerable<T> collection)
        {
            return collection.ToStringJoin(null);
        }

        /// <summary>
        /// Returns the value for the key or the default value.
        /// </summary>
        /// <typeparam name="TKey">The key type</typeparam>
        /// <typeparam name="TValue">The value type</typeparam>
        /// <param name="dictionary">The dictionary to lookup</param>
        /// <param name="key">The key to use in the lookup</param>
        /// <returns>
        /// If the dictionary contains the key then the value associated with that key will be retuned. Otherwise a default value
        /// object will be returned.
        /// </returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default(TValue);
        }


        /// <summary>
        /// Returns the first index for which the index selector returns true.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="collection">The collection</param>
        /// <param name="indexSelectorFunc">The index selector function</param>
        /// <returns></returns>
        public static int IndexOf<TValue>(this IList<TValue> collection, Func<TValue, int, bool> indexSelectorFunc)
        {
            for (var i = 0; i < collection.Count; i++)
            {
                if (indexSelectorFunc(collection[i], i))
                {
                    return i;
                }
            }
            return -1;
        }


        /// <summary>
        /// Returns the first index for which the index selector returns true.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="collection">The collection</param>
        /// <param name="indexSelectorFunc">The index selector function</param>
        /// <returns></returns>
        public static int IndexOf<TValue>(this IList<TValue> collection, Func<TValue, bool> indexSelectorFunc)
        {
            for (var i = 0; i < collection.Count; i++)
            {
                if (indexSelectorFunc(collection[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Convenience method for adding a group of items to a collection.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="collection"></param>
        /// <param name="values"></param>
        public static void AddRange<TValue>(this ICollection<TValue> collection, IEnumerable<TValue> values)
        {
            foreach (var value in values)
            {
                collection.Add(value);
            }
        }

        public static ObservableCollection<TValue> ToObservableCollection<TValue>(this IEnumerable<TValue> source)
        {
            return new ObservableCollection<TValue>(source);
        }

        public static TInput AggregateOrDefault<TInput>(this ICollection<TInput> source, Func<TInput, TInput, TInput> func)
        {
            return source.Any() ? source.Aggregate(func) : default(TInput);
        }

        public static HashSet<TValue> ToHashSet<TValue>(this IEnumerable<TValue> source)
        {
            return new HashSet<TValue>(source);
        }

        public static void Sort<TValue, TKey>(this ObservableCollection<TValue> collection, Func<TValue, TKey> keySelector) where TKey : IComparable
        {
            var sorted = collection.OrderBy(keySelector).ToList();
            for (var i = 0; i < sorted.Count; i++)
                collection.Move(collection.IndexOf(sorted[i]), i);
        }

        public static IEnumerable<TResult> SelectAtIntersection<TResult, TValue1, TValue2>(
            this IEnumerable<TValue1> first, IEnumerable<TValue2> second, Func<TValue1, TValue2, bool> predicate,
            Func<TValue1, TValue2, TResult> selector)
        {
            return from one in first from two in second where predicate(one, two) select selector(one, two);
        }


        public static IList<TValue> ToList<TValue>(this ICollection collection)
        {
            var list = new List<TValue>();
            foreach (var item in collection)
            {
                list.Add((TValue)item);
            }
            return list;
        }

        public static IEnumerable<TValue> NullAsEmpty<TValue>(this IEnumerable<TValue> collection)
        {
            return collection ?? System.Linq.Enumerable.Empty<TValue>();
        }

        public static IEnumerable<TValue> Distinct<TValue>(this IEnumerable<TValue> collection, Func<TValue, object> selector)
        {
            return collection.Distinct(new DelegateEqualityComparer<TValue>(selector));
        }
    }

}
