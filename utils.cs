using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

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
        public struct _WithIndex<T> {
            public int Idx;
            public T Val;
        }
        public static IEnumerable< _WithIndex<T> > WithIndex<T>(this IEnumerable<T> seq) {
            return seq.Select((val,idx) => new _WithIndex<T> {Idx=idx, Val=val});
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

        // From http://stackoverflow.com/a/3453301
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N) {
            return source.Skip(Math.Max(0, source.Count() - N));
        }

        public static void PyREPL(params object[] vars) {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            for (int i = 0; i < vars.Length; i += 2) {
                string key = (string) vars[i];
                scope.SetVariable(key, vars[i+1]);
            }
            var code = "try: import sys; sys.ps1='\\n\\n'+sys.ps1+'\\n'; sys.path.append('/usr/lib/ipy/Lib');import code; code.interact(None,None,locals())\nexcept: __import__('traceback').print_exc()";
            var source = engine.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
            source.Execute(scope);
        }


        public class CompactTableBuilder<TFull, TCompact> {
            Dictionary<TFull, int> map = new Dictionary<TFull, int>();
            Func<TFull, TCompact> compactFunc;

            public CompactTableBuilder(Func<TFull, TCompact> compactFunc, IEnumerable<TFull> items = null) {
                this.compactFunc = compactFunc;
                if (items != null) this.Add(items);
            }

            public int Add(TFull obj) {
                return this.map.SetDefault(obj, this.map.Count);
            }

            public void Add(IEnumerable<TFull> objs) {
                foreach (var obj in objs) this.Add(obj);
            }

            public int GetId(TFull obj) {
                return this.map[obj];
            }

            public TCompact[] BuildTable() {
                var table = new TCompact[this.map.Count];
                foreach (var itm in this.map) {
                    table[itm.Value] = this.compactFunc(itm.Key);
                }
                return table;
            }

            public int Count {
                get { return this.map.Count; }
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


        public static void WriteUInt24(this BinaryWriter wr, uint num) {
            wr.Write((byte) (num & 0xff));
            wr.Write((byte) ((num >> 8) & 0xff));
            wr.Write((byte) ((num >> 16) & 0xff));
        }
        public static uint ReadUInt24(this BinaryReader rd) {
            uint r = 0;
            for (int i = 0; i < 3; i++) {
                r <<= 8;
                r |= rd.ReadByte();
            }
            return r;
        }


        public class Profiler  : IDisposable {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            string desc;
            public Profiler(string desc) {
                this.stopwatch.Start();
                this.desc = desc;
            }

            public void Dispose() {
                this.stopwatch.Stop();
                Dbg(string.Format("[{0}.{1:D3}] {2}", this.stopwatch.ElapsedMilliseconds/1000,
                            this.stopwatch.ElapsedMilliseconds%1000, this.desc));
            }
        }

        /// Remove diacritic marks (accents) from string.
        ///
        /// Taken from http://archives.miloush.net/michkap/archive/2007/05/14/2629747.html.
        public static string RemoveDiacritics(string s) {
            string nfd = s.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            for(int i = 0; i < nfd.Length; i++) {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(nfd[i]);
                if(uc != UnicodeCategory.NonSpacingMark) {
                    sb.Append(nfd[i]);
                }
            }

            return(sb.ToString().Normalize(NormalizationForm.FormC));
        }

        public static void Dbg(params object[] args) {
            Console.Error.WriteLine("DEBUG: " + string.Join(" ", args.Select(x => x.ToString())));
        }
    }
}
