using System;
using System.Collections.Generic;
namespace CCon {
    static class Utils {
        public static IEnumerable< Tuple<T,T> > Pairs<T>(this IEnumerable<T> seq) {
            T last = default(T);
            bool first = true;
            foreach(T itm in seq) {
                if (first) first = false;
                else yield return new Tuple<T,T>(last,itm);
                last = itm;
            }
        }
        // From http://stackoverflow.com/a/1514470
        public static V SetDefault<K,V>(this IDictionary<K,V> dict, K key, V dfl) {
            V val;
            if (!dict.TryGetValue(key, out val)) {
                dict.Add(key, dfl);
                return dfl;
            } else {
                return val;
            }
        }
        public static V SetDefault<K,V>(this IDictionary<K,V> dict, K key, Func<V> dfl) {
            V val;
            if (!dict.TryGetValue(key, out val)) {
                val = dfl();
                dict.Add(key, val);
            }
            return val;
        }
    }
}
