using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using CommandLine;
using CommandLine.Text;

using static CCon.Model;
using static CCon.Utils;

//HACK to get this into help message
[assembly:AssemblyCopyright("Usage: ccon [options] [(-d|-a) HH:MM] [-D DATE] [-v VIA] FROM TO")]
[assembly:AssemblyVersion("0.1")]


namespace CCon {
    public class StopNotFound : UserError {
        public string Query;
        public StopNotFound(string query) : base(string.Format("Stop not found: '{0}'", query)) {
            this.Query = query;
        }
    }
    public class StopAmbiguous : UserError {
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

        [Option("db", MetaValue="FILE", HelpText="Path to timetable database created by ccon-build [default:~/.cache/ccon.dat]")]
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
        Dictionary<string, string> aliases = new Dictionary<string, string>();

        CLI(Model model = null) {
            this.model = model;
        }

        /// Split a stop name into normalized words, removing accents and punctuation.
        public static string[] SplitStop(string name) {
            var words = NormalizeStopName(name).Split(' ');
            return words;
        }

        /// Find a stop matching a query, returning the IDs of all its substops.
        ///
        /// Each word of the query must be a prefix of the corresponding word
        /// of the stop name. Among matches satisfying this criterion, a scoring
        /// is established. Whole-word matches (instead of just prefixes) give
        /// +1 point, extra words at the end of the stop name give -1 point.
        ///
        /// If there is a unique highest-score name, it is returned. Otherwise,
        /// StopAmbiguous is raised.
        ///
        /// Interpunction is considered equivalent to whitespace.
        ///
        /// This behaves rather intuitively. Examples:
        ///    * pelc       -> Pelc-Tyrolka
        ///    * malos      -> Malostranská
        ///    * malos-n    -> Malostranské náměstí
        ///    * hl-n       -> Hlavní nádraží
        ///    * zoo        -> Zoologická zahrada
        /// but:
        ///    * narod      -> AMBIGUOUS (Národní divadlo, Národní třída)
        public ushort[] FindStop(string query) {
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

        static readonly Regex StopDistRE = new Regex(@"([^+]*)(?:\+([0-9]+))?");


        public string ResolveAlias(string s) {
            if (this.aliases.ContainsKey(s)) return this.aliases[s];
            else return s;
        }

        /// Parse the a virtual stop definition.
        ///
        /// Expects a string in the format "kuchynka+5/trojska+10/pelc+3".
        public StopDistance[] FindStopDists(string s) {
            List<StopDistance> ret = new List<StopDistance>();
            var variants = s.Split('/');
            foreach (var variant in variants) {
                var m = StopDistRE.Match(variant);
                string stopName = m.Groups[1].Value;
                string timeDistStr = m.Groups[2].Value;
                if (timeDistStr == "") timeDistStr = "0";
                ushort timeDist = (ushort) (Convert.ToUInt16(timeDistStr) * 60 / TimeGranularity);
                foreach (var stopId in this.FindStop(stopName)) {
                    ret.Add(new StopDistance { Stop=stopId, TimeDist=timeDist });
                }
            }
            return ret.ToArray();
        }

        static string timeForPrint(ushort tm) {
            if (tm == ushort.MaxValue) return "";
            int sec = tm * TimeGranularity;
            int min = sec/60; // rounds down, which is usually what we want
            return string.Format("{0,2}:{1:D2}", min/60, min%60);
        }

        public void PrintConnection(Connection conn) {
            ushort lastArrTime = ushort.MaxValue;
            var V = this.model.Graph.Vertices;

            var segs = conn.Segments.Select( seg =>
                    new {
                        FromName = this.model.Stops[V[seg.Start].Stop].Name,
                        ToName = this.model.Stops[V[seg.End].Stop].Name,
                        StartTime = V[seg.Start].Time,
                        EndTime = V[seg.End].Time,
                        RouteShortName = this.model.CalRoutes[V[seg.Start].CalRoute].RouteShortName,
                    }
            ).ToList();

            if (segs[0].StartTime != conn.StartTime) {
                segs.Insert(0, new {
                        FromName = "(Start)",
                        ToName = segs[0].FromName,
                        StartTime = conn.StartTime,
                        EndTime = segs[0].StartTime,
                        RouteShortName = "", //"(walk)",
                    });
            }
            var lastSeg = segs[segs.Count - 1];
            if (lastSeg.EndTime != conn.EndTime) {
                segs.Add(new {
                        FromName = lastSeg.ToName,
                        ToName = "(Destination)",
                        StartTime = lastSeg.EndTime,
                        EndTime = conn.EndTime,
                        RouteShortName = "", //"(walk)",
                    });
            }

            var tab = segs.Select(seg => {
                    var row = new {
                        StopName = seg.FromName,
                        ArrTime = lastArrTime,
                        DepTime = seg.StartTime,
                        RouteShortName = seg.RouteShortName,
                    };
                    lastArrTime = seg.EndTime;
                    return row;
            }).ToList();
            tab.Add(new {
                StopName = segs.Last().ToName,
                ArrTime = lastArrTime,
                DepTime = ushort.MaxValue,
                RouteShortName = "",
            });

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

        void LoadConfig() {
            var fn = GetEnv("HOME") + "/.config/ccon.conf";
            if (!File.Exists(fn)) return;
            using (var sr = new StreamReader(fn)) {
                string line;
                while ((line = sr.ReadLine()) != null) {
                    line = line.Trim();
                    if (line == "") continue;
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();
                    this.aliases[key] = val;
                }

            }
        }

        void Run(Arguments args) {
            this.LoadConfig();
            this.model = Model.Load(args.ModelPath ?? GetEnv("CCON_DB") ?? (GetEnv("HOME")+"/.cache/ccon.dat"));
            StopDistance[] from = FindStopDists(ResolveAlias(args.From));
            StopDistance[] to = FindStopDists(ResolveAlias(args.To));
            var router = new Router(this.model, args.Date);
            IEnumerable<Connection> conns;
            using (new Profiler("Find connection")) {
                if (args.Via != null) {
                    var via = FindStopDists(ResolveAlias(args.Via));
                    conns = router.FindVia(from, via, to);
                } else {
                    conns = router.FindConnections(from, to);
                }
            }
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
            try {
                (new CLI()).Run(Arguments.Parse(args));
            } catch (Exception ex) {
                if (ex is TargetInvocationException) {
                    ex = ((TargetInvocationException)ex).InnerException;
                }
                while (ex is ApplicationException) {
                    ex = ((ApplicationException)ex).InnerException;
                }
                if (ex is UserError || ex is IOException || ex is FormatException) {
                    Console.Error.WriteLine("ccon: error: " + ex.Message);
                    Environment.Exit(1);
                } else {
                    throw;
                }
            }
        }

    }
}
