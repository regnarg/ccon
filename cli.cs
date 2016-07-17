using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using static CCon.Model;
using static CCon.Utils;

namespace CCon {
    public class StopNotFound : Exception {
        public string Query;
        public StopNotFound(string query) : base(string.Format("Stop not found: '{0}'", query)) {
            this.Query = query;
        }
    }
    public class StopAmbiguous : Exception {
        public string Query;
        public string[] Matches;
        public StopAmbiguous(string query, string[] matches) : base(string.Format("Stop ambiguous: '{0}'.\nMatches: '{1}'",
                                                                    query, string.Join("', '", matches))) {
            this.Query = query;
            this.Matches = matches;
        }
    }
    class CLI {
        Model model;

        CLI(Model model) {
            this.model = model;
        }

        static Regex StopSpace = new Regex(@"[^a-z0-9]+");
        /// Split a stop name into normalized words, removing accents and punctuation.
        public static string[] SplitStop(string name) {
            name = RemoveDiacritics(name);
            name = name.ToLower();
            name = StopSpace.Replace(name, " ");
            name = name.Trim();
            var words = name.Split(' ');
            return words;
        }

        ushort[] FindStop(string query) {
            var qwords = SplitStop(query);
            int bestScore = int.MinValue;
            SortedSet<string> bestNames = new SortedSet<string>();
            List<ushort> bestIds = new List<ushort>();
            foreach (var stop in this.model.Stops.WithIndex()) {
                int score = 0;
                var words = SplitStop(stop.Val.Name);
                if (qwords.Length > words.Length) continue;
                if (!Enumerable.Zip(words, qwords, (w,qw)=>w.StartsWith(qw)).All(x=>x)) continue;

                // Prefer exact word matches to prefixes (+1 point for each exact match)
                score += Enumerable.Zip(words, qwords, (w,qw)=>(w==qw ? 1 : 0)).Sum();
                // Penalize extra words at the end (-1 point for each word not in the query)
                score -= (words.Length - qwords.Length);

                Debug("Stop", stop.Val.Name, "score", score);

                if (score > bestScore) {
                    bestScore = score;
                    bestNames = new SortedSet<string>();
                    bestIds = new List<ushort>();
                }
                if (score == bestScore) {
                    bestNames.Add(stop.Val.Name);
                    bestIds.Add((ushort) stop.Idx);
                }
            }

            if (bestIds.Count == 0) {
                throw new StopNotFound(query);
            }
            if (bestNames.Count > 1) {
                throw new StopAmbiguous(query, bestNames.ToArray());
            }

            return bestIds.ToArray();
        }

        void Run(string[] args) {
            ushort[] from = FindStop(args[1]);
            ushort[] to = FindStop(args[2]);
            var router = new Router(this.model);
            using (new Profiler("Find connection"))
                router.FindConnection(from, to);
        }

        static void Main(string[] args) {
            var model = Model.Load(args[0]);
            (new CLI(model)).Run(args);
        }

    }
}
