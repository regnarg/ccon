using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using CommandLine;
using CommandLine.Text;

using static CCon.Model;
using static CCon.Utils;

//HACK to get this into help message
[assembly:AssemblyCopyright("Usage: ccon [options] [(-d|-a) HH:MM] [-D DATE] [-v VIA] FROM TO")]
[assembly:AssemblyVersion("0.1")]


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

    class Arguments {
        [Value(0, MetaValue="FROM", HelpText="Starting stop", Required=true)]
        public string From { get; set; }

        [Value(1, MetaValue="TO", HelpText="Destination stop", Required=true)]
        public string To { get; set; }

        [Option('v', MetaValue="STOP", HelpText="Travel via STOP")]
        public string Via { get; set; }

        public ushort DepTime = ushort.MaxValue, ArrTime = ushort.MaxValue;

        [Option('d', "dep", MetaValue="HH:MM", HelpText="Departure time (earliest possible)")]
        public string DepTimeStr { set { this.DepTime = ParseTime(value+":00"); } }

        [Option('a', "arr", MetaValue="HH:MM", HelpText="Arrival time (latest possible)")]
        public string ArrTimeStr { set { this.ArrTime = ParseTime(value+":00"); } }

        [Option('A', "all", HelpText="Do not limit number of shown connections.")]
        public bool ShowAll { get; set; }

        public DateTime Date;
        [Option('D', "date", MetaValue="DATE", HelpText="Travel date")]
        public string DateStr { set { this.Date = DateTime.Parse(value).Date; } }

        [Option("db", MetaValue="FILE.dat", HelpText="Path to timetable database (created by ccon-build)",
                Default="~/.cache/ccon.dat")]
        public string ModelPath { get; set; }

        public static Arguments Parse(string[] args) {
            var res =  Parser.Default.ParseArguments<Arguments>(args) as Parsed<Arguments>;
            if (res == null) {
                Environment.Exit(1);
            }
            return res.Value;
        }
    };

    class CLI {
        Model model;
        const int ShowConnections = 5; // how many connections to show by default

        CLI(Model model = null) {
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

                Dbg("Stop", stop.Val.Name, "score", score);

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

        static string timeForPrint(ushort tm) {
            if (tm == ushort.MaxValue) return "";
            int sec = tm * TimeGranularity;
            int min = sec/60; // rounds down, which is usually what we want
            return string.Format("{0}:{1:D2}", min/60, min%60);
        }

        public void PrintConnection(Connection conn) {
            ushort lastArrTime = ushort.MaxValue;
            var V = this.model.Graph.Vertices;
            var tab = conn.Segments.Select(seg => {
                    var row = new {
                        StopName = this.model.Stops[V[seg.Start].Stop].Name,
                        ArrTime = lastArrTime,
                        DepTime = V[seg.Start].Time,
                        RouteShortName = this.model.CalRoutes[V[seg.Start].CalRoute].RouteShortName,
                    };
                    lastArrTime = V[seg.End].Time;
                    return row;
            }).Concat((new []{0}).Select(x=> new {
                StopName = this.model.Stops[V[conn.Segments.Last().End].Stop].Name,
                ArrTime = lastArrTime,
                DepTime = ushort.MaxValue,
                RouteShortName = "",
            }));

            string bold = "\x1b[1m{0}\x1b[0m";

            foreach (var row in tab) {
                var arrString = timeForPrint(row.ArrTime);
                var depString = timeForPrint(row.DepTime);
                // Format the original departure and final arrival bright and bold
                if (row.ArrTime == ushort.MaxValue) depString = string.Format(bold, depString);
                if (row.DepTime == ushort.MaxValue) arrString = string.Format(bold, arrString);
                Console.WriteLine("{0,-25}  {1,5}  {2,5}  {3}", row.StopName,
                        arrString, depString,
                        row.RouteShortName);
            }
        }

        void Run(Arguments args) {
            this.model = Model.Load(args.ModelPath);
            ushort[] from = FindStop(args.From);
            ushort[] to = FindStop(args.To);
            var router = new Router(this.model, args.Date);
            IEnumerable<Connection> conns;
            using (new Profiler("Find connection"))
                conns = router.FindConnections(from, to);
            if (args.ArrTime == ushort.MaxValue && args.DepTime == ushort.MaxValue) {
                args.DepTime = (ushort) (DateTime.Now.TimeOfDay.TotalSeconds / TimeGranularity);
            }
            if (args.DepTime != ushort.MaxValue) {
                conns = conns.Where(conn => conn.StartTime >= args.DepTime);
                if (args.ArrTime == ushort.MaxValue && !args.ShowAll) conns = conns.Take(ShowConnections);
            }
            if (args.ArrTime != ushort.MaxValue) {
                conns = conns.Where(conn => conn.EndTime <= args.ArrTime);
                if (args.DepTime == ushort.MaxValue && !args.ShowAll) conns = conns.TakeLast(ShowConnections);
            }
            foreach (var conn in conns) {
                Console.WriteLine("------------------------------------------------------------------");
                this.PrintConnection(conn);
            }
        }

        static void Main(string[] args) {
            (new CLI()).Run(Arguments.Parse(args));
        }

    }
}
