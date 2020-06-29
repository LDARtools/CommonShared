using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ldartools.Common.Extensions.Enumerable
{
    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            return dictionary.ContainsKey(key) ? dictionary[key] : defaultValue;
        }

        public static TValue GetValueOrInit<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> initFunc)
        {
            if(initFunc == null) throw new ArgumentNullException(nameof(initFunc));
            return dictionary.ContainsKey(key) ? dictionary[key] : initFunc(key);
        }

        public static void AddToList<TKey, TValue, TList>(this Dictionary<TKey, TList> dictionary, TKey key, TValue value) where TList : IList<TValue>, new()
        {
            if (!dictionary.TryGetValue(key, out var list))
            {
                list = new TList();
                dictionary.Add(key, list);
            }
            list.Add(value);
        }

        public static void RemoveAll<TKey, TValue, TList>(this Dictionary<TKey, TList> dictionary, Func<TValue, bool> selector, bool removeEmptyList = false)
            where TList : IList<TValue>
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            List<TKey> emptyLists = null;
            foreach (var pair in dictionary)
            {
                var list = pair.Value;
                var index = 0;
                while (index < list.Count)
                {
                    if (selector(list[index]))
                    {
                        list.RemoveAt(index);
                    }
                    else
                    {
                        index++;
                    }
                }
                if (list.Count == 0)
                {
                    if (emptyLists == null)
                    {
                        emptyLists = new List<TKey>();
                    }
                    emptyLists.Add(pair.Key);
                }
            }
            if (!removeEmptyList || emptyLists == null) return;
            foreach (var key in emptyLists)
            {
                dictionary.Remove(key);
            }
        }

        [Obsolete("Use CaseInsensitiveComparer instead.")]
        public static TValue GetValueIgnoreCase<TValue>(this Dictionary<string, TValue> dict, string key)
        {
            return dict.Single(kvp => key.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        [Obsolete("Use CaseInsensitiveComparer instead.")]
        public static bool ContainsKeyIgnoreCase<TValue>(this Dictionary<string, TValue> dict, string key)
        {
            return dict.Any(kvp => key.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
        }

        public static object GetValueOrDefault(this Hashtable table, object key, object defaultValue = default(object))
        {
            return table.ContainsKey(key) ? table[key] : defaultValue;
        }
    }
}
