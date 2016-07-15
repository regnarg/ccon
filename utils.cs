using System;
using System.Collections.Generic;

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
            var code = "try: import sys; sys.ps1='\\n\\n'+sys.ps1; sys.path.append('/usr/lib/ipy/Lib');import code; code.interact(None,None,locals())\nexcept: __import__('traceback').print_exc()";
            var source = engine.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
            source.Execute(scope); 
        }
    }
}
