using System;
using System.Collections.Generic;
using System.Linq;

using IronPython.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

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
        public struct WithIndex<T> {
            public int Idx;
            public T Val;
        }
        public static IEnumerable< WithIndex<T> > Indexed<T>(this IEnumerable<T> seq) {
            return seq.Select((val,idx) => new WithIndex<T> {Idx=idx, Val=val});
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
        
        public static void PyREPL(params object[] vars) {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            foreach (var pair in vars.Pairs()) {
                string key = (string) pair.Item1;
                
                scope.SetVariable((string) pair.Item1, pair.Item2);
            }
			// sys.path.insert(0,'/data/mff/4V/cs-zap/ipython/Lib');import readline; 
            var code = "try: import sys; sys.ps1='\\n\\n'+sys.ps1+'\\n'; sys.path.append('/usr/lib/ipy/Lib');import code; code.interact(None,None,locals())\nexcept: __import__('traceback').print_exc()";
            var source = engine.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
            source.Execute(scope); 
        }


        public class CompactTableBuilder<TFull, TCompact> {
            List<TCompact> table = new List<TCompact>();
            Dictionary<TFull, int> map = new Dictionary<TFull, int>();
            Func<TFull, TCompact> compactFunc;

            public CompactTableBuilder(Func<TFull, TCompact> compactFunc) {
                this.compactFunc = compactFunc;
            }

            public int Add(TFull obj) {
                return this.map.SetDefault(obj, delegate {
                            this.table.Add(this.compactFunc(obj));
                            return this.table.Count - 1;
                        });
            }

            public TCompact[] GetTable() {
                return this.table.ToArray();
            }

            public int Count {
                get { return this.table.Count; }
            }
        }

        public const int TimeGranularity = 5; ///< Number of seconds that make up one time unit

        /**
         *  Convert a GTFS-like HH:MM:SS time to seconds/5-since-midnight.
         *
         *  This is done in order to fit the times into a ushort,
         *  which should save about 3MB of HDD/RAM for the DPP data.
         *  And it is sufficient: DPP has 5s granularity, ČD 30s.
         *
         *  GTFS time is an ordinary HH:MM:SS string with the unusual
         *  property that it can go over midnight (e.g. 24:30).
         *  Standard TimeSpan class rather stupidly parses this as
         *  24 days and 30 minutes!
         */
        public static ushort ParseTime(string s) {
            int sec = 0;
            string[] comps = s.Split(':');
            foreach (string comp in comps) {
                sec = sec*60 + int.Parse(comp);
            }
            return (ushort) (sec / TimeGranularity);
        }

        public static string FormatTime(ushort t) {
            int sec = t * TimeGranularity;
            string[] comps = new string[3];
            for (int i = 2; i >= 0; i--) {
                comps[i] = (sec % 60).ToString(i == 0 ? "D1" : "D2");
                sec /= 60;
            }
            return string.Join(":", comps);
        }

    }


}
