using System;
using System.IO;
using LINQtoCSV;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;

using CommandLine;
using CommandLine.Text;

using static CCon.Utils;

//HACK to get this into help message
[assembly:AssemblyCopyright("Usage: kango2gtfs KANGO-BASENAME OUT-DIR")]
[assembly:AssemblyVersion("0.1")]

namespace CCon {
    /// A parser for the KANGO transit format for Czech train timetables.
    /// Identifiers and descriptions are in Czech as they use the KANGO terminology.
    public class Kango {
        const int CzechRepublic = 54; ///< the railway code for the CR

        public class Arguments {
            [Value(0, MetaValue="KANGO-BASENAME", HelpText="The common basename (without extension) of the input KANGO files.", Required=true)]
            public string KangoPrefix { get; set; }

            [Value(1, MetaValue="OUT-DIR", HelpText="Output directory to save GTFS files to.", Required=true)]
            public string OutDir { get; set; }

            public static Arguments Parse(string[] args) {
                var res =  Parser.Default.ParseArguments<Arguments>(args) as Parsed<Arguments>;
                if (res == null) {
                    Environment.Exit(1);
                }
                return res.Value;
            }
        };
        

        public class Invalid : UserError {
            public Invalid(string msg) : base("Error reading KANGO file: " + msg) {}
        }

        public static readonly CsvFileDescription CSVDesc = new CsvFileDescription {
                    SeparatorChar = '|',
                    FirstLineHasColumnNames = false,
                    EnforceCsvColumnAttribute = true,
                    IgnoreUnknownColumns = true,
                    // Needed to parse floats with decimal points, regardless of locale
                    FileCultureName = "",
                    MaximumNbrExceptions = 1,
                    TextEncoding = Encoding.GetEncoding("windows-1250"), // seriously?
                };

        /// The role of a train on a part of its route.
        ///
        /// There may be several TrainType records for the same train number: e.g.
        /// for a part of its route the train offers passenger service, another part
        /// is manipulation-only (e.g. going to the depot).
        public class TrainType { // *.dvl
            [CsvColumn(FieldIndex=1)]
            public string TrainNumber;
            [CsvColumn(FieldIndex=2)]
            public int Country;

            [CsvColumn(FieldIndex=3)]
            public string dummy3;

            [CsvColumn(FieldIndex=4)]
            public int FromStation;

            [CsvColumn(FieldIndex=5)]
            public string dummy5;
            [CsvColumn(FieldIndex=6)]
            public string dummy6;

            [CsvColumn(FieldIndex=7)]
            public int ToStation;

            [CsvColumn(FieldIndex=8)]
            public string dummy8;

            [CsvColumn(FieldIndex=9)]
            public int CalendarId;

            [CsvColumn(FieldIndex=10)]
            public string Type; ///< Regional train (Os, Sp), fast train (R), Ex, EC, IC, ...

            [CsvColumn(FieldIndex=11)]
            public string Category; ///< Passenger public (ODv), private (ODn) or freight (ND) train?
        }

        public class Station { // *.db
            [CsvColumn(FieldIndex=1)]
            public int Country;

            [CsvColumn(FieldIndex=2)]
            public int Id;

            [CsvColumn(FieldIndex=3)]
            public int SubId;

            [CsvColumn(FieldIndex=4)]
            public string Name;
        }

        public class TrainStopTime {
            [CsvColumn(FieldIndex=1)]
            public string TrainNumber;

            [CsvColumn(FieldIndex=2)]
            public string dummy2;

            [CsvColumn(FieldIndex=3)]
            public int StationId;

            [CsvColumn(FieldIndex=4)]
            public string dummy4;
            [CsvColumn(FieldIndex=5)]
            public string dummy5;
            [CsvColumn(FieldIndex=6)]
            public string dummy6;
            [CsvColumn(FieldIndex=7)]
            public string dummy7;

            [CsvColumn(FieldIndex=8)]
            public int ArrDay = -1;
            [CsvColumn(FieldIndex=9)]
            public int ArrHour = -1;
            [CsvColumn(FieldIndex=10)]
            public int ArrMin = -1;
            [CsvColumn(FieldIndex=11)]
            public int ArrHalfMin = -1;

            [CsvColumn(FieldIndex=12)]
            public string dummy12;
            [CsvColumn(FieldIndex=13)]
            public string dummy13;

            [CsvColumn(FieldIndex=14)]
            public int DepDay = -1;
            [CsvColumn(FieldIndex=15)]
            public int DepHour = -1;
            [CsvColumn(FieldIndex=16)]
            public int DepMin = -1;
            [CsvColumn(FieldIndex=17)]
            public int DepHalfMin = -1;

            [CsvColumn(FieldIndex=18)]
            public string dummy18;
            [CsvColumn(FieldIndex=19)]
            public string dummy19;
            [CsvColumn(FieldIndex=20)]
            public string dummy20;
            [CsvColumn(FieldIndex=21)]
            public string dummy21;
            [CsvColumn(FieldIndex=22)]
            public string dummy22;
            [CsvColumn(FieldIndex=23)]
            public string dummy23;
            [CsvColumn(FieldIndex=24)]
            public string dummy24;
            [CsvColumn(FieldIndex=25)]
            public string dummy25;
            [CsvColumn(FieldIndex=26)]
            public string dummy26;
            [CsvColumn(FieldIndex=27)]
            public string dummy27;
            [CsvColumn(FieldIndex=28)]
            public string dummy28;
            [CsvColumn(FieldIndex=29)]
            public string dummy29;
            [CsvColumn(FieldIndex=30)]
            public string dummy30;
            [CsvColumn(FieldIndex=31)]
            public string dummy31;
            [CsvColumn(FieldIndex=32)]
            public string dummy32;

            [CsvColumn(FieldIndex=33)]
            public int manipOnlyStop;

            [CsvColumn(FieldIndex=34)]
            public string dummy34;
            [CsvColumn(FieldIndex=35)]
            public string dummy35;
            [CsvColumn(FieldIndex=36)]
            public string dummy36;
            [CsvColumn(FieldIndex=37)]
            public string dummy37;
            [CsvColumn(FieldIndex=38)]
            public string dummy38;
            [CsvColumn(FieldIndex=39)]
            public string dummy39;
            [CsvColumn(FieldIndex=40)]
            public string dummy40;
            [CsvColumn(FieldIndex=41)]
            public string dummy41;
            [CsvColumn(FieldIndex=42)]
            public string dummy42;
            [CsvColumn(FieldIndex=43)]
            public string dummy43;
            [CsvColumn(FieldIndex=44)]
            public string dummy44;
            [CsvColumn(FieldIndex=45)]
            public string dummy45;
            [CsvColumn(FieldIndex=46)]
            public string dummy46;
            [CsvColumn(FieldIndex=47)]
            public string dummy47;
            [CsvColumn(FieldIndex=48)]
            public string dummy48;
            [CsvColumn(FieldIndex=49)]
            public string dummy49;
        }

        Dictionary<int, Station> stations;

        List<T> LoadCSV<T>(string path) where T: class, new() {
            using (new Profiler("Loading " + path)) {
                var cc = new CsvContext();
                try {
                    return cc.Read<T>(path, CSVDesc).ToList();
                } catch(AggregatedException ae) {
                    string msg;
                    // Process all exceptions generated while processing the file
                    List<Exception> innerExceptionsList =
                        (List<Exception>)ae.Data["InnerExceptionsList"];
                    msg = "Error reading " + path + "\n" + String.Join("\n",
                            innerExceptionsList.Select(exc => exc.Message));
                    throw new Invalid(msg);
                }
            }
        }

        /// Cut the part of a train path between stations `fromStation` and `toStation` (both included).
        /// This differs from a combination of SkipWhile/TakeWhile, which would not include `toStation`.
        public IEnumerable<TrainStopTime> CutPath(IEnumerable<TrainStopTime> wholePath, int fromStation, int toStation) {
            bool inRange = false;
            bool first = false;
            foreach (var stopTime in wholePath) {
                if (stopTime.StationId == fromStation) { inRange = true; first = true; }
                // Stops without arrival time filled in are just passed without stopping.
                // (with the exception of the first stop, which naturally has not arrival time)
                if (inRange && (first || stopTime.ArrHour != -1) && this.stations.ContainsKey(stopTime.StationId))
                    yield return stopTime;
                first = false;
                if (stopTime.StationId == toStation) inRange = false;
            }
        }

        public static readonly Regex NormalizeRemove = new Regex(@"(\s*\bos\.n\.|\s*\bz$)");
        /// Normalize a station name by removing low-level railway term and acronyms.
        ///
        /// E.g. remove the "os.n." (passenger station) or " z" (stop, not full station)
        /// suffixes.
        public static string NormalizeName(string name) {
            name = NormalizeRemove.Replace(name, "");
            return name;
        }

        public static ushort ConvertTime(int day, int hour, int minute, int halfMin) {
            if (hour == -1) return ushort.MaxValue;
            return (ushort) ((day*24*3600 + hour*3600 + minute*60 + halfMin*30) / TimeGranularity);
        }

        /// Load KANGO data.
        ///
        /// The data consists of several files differing only by extension (*sigh* I know),
        /// therefore you pass just the common prefix without the extension.
        public void Process(string namePrefix, string outDir) {
            this.stations = this.LoadCSV<Station>(namePrefix+".db").Where(x => x.Country == CzechRepublic && x.SubId==0).ToDictionary(x => x.Id);
            // Select only public passenger trains. Drop Sv ("soupravový vlak" - manipulation rides).
            var trainTypes = this.LoadCSV<TrainType>(namePrefix+".dvl").Where(
                    x => x.Country == CzechRepublic && x.Category == "ODv" && x.Type != "Sv").ToList();
            var trainStopTimes = this.LoadCSV<TrainStopTime>(namePrefix+".trv").Where(
                        x => x.manipOnlyStop==0
                    ).GroupBy(x => x.TrainNumber).ToDictionary(g=>g.Key, g=>g.ToList());

            foreach (var station in stations.Values) {
                station.Name = NormalizeName(station.Name);
            }

            Dictionary<string, int> trainNumRepeatIndex = new Dictionary<string, int>();

            SortedSet<int> usedStops = new SortedSet<int>();
            List<GTFS.Route> gtfsRoutes = new List<GTFS.Route>();
            List<GTFS.Trip> gtfsTrips = new List<GTFS.Trip>();
            List<GTFS.StopTime> gtfsStopTimes = new List<GTFS.StopTime>();
            List<GTFS.Calendar> gtfsCalendars = new List<GTFS.Calendar>();

            gtfsCalendars.Add(new GTFS.Calendar {
                        Id = "fake",
                        Monday = 1,
                        Tuesday = 1,
                        Wednesday = 1,
                        Thursday = 1,
                        Friday = 1,
                        Saturday = 1,
                        Sunday = 1,
                        Start = new DateTime(2000,1,1),
                        End = new DateTime(2100,1,1),
                    });

            foreach (var trainType in trainTypes) {
                var wholePath = trainStopTimes[trainType.TrainNumber];
                var path = CutPath(wholePath, trainType.FromStation, trainType.ToStation).ToList();
                if (path.Count == 0) {
                    Dbg("Empty path for train", trainType.Type, trainType.TrainNumber);
                    continue;
                }

                // The same train number may appear more than once, append a counter if necessary.
                string gtfsId = trainType.TrainNumber;
                int idx = trainNumRepeatIndex.SetDefault(gtfsId, 0);
                if (idx > 0) gtfsId = string.Format("{0}_{1}", gtfsId, idx);
                trainNumRepeatIndex[trainType.TrainNumber]++;

                var route = new GTFS.Route {
                    Id = gtfsId,
                    // For some reasons, the train nums in KANGO are in the form "777/7".
                    // The part after the slash is always unique, no idea what it is for.
                    ShortName = trainType.Type + " " + trainType.TrainNumber.Split('/')[0],
                };

                var trip = new GTFS.Trip {
                    Id = gtfsId,
                    RouteId = gtfsId,
                    CalendarId = "fake",
                };

                gtfsRoutes.Add(route);
                gtfsTrips.Add(trip);
                gtfsStopTimes.AddRange(path.Select((itm,seq) => {
                    ushort depTime = ConvertTime(itm.DepDay, itm.DepHour, itm.DepMin, itm.DepHalfMin);
                    ushort arrTime = ConvertTime(itm.ArrDay, itm.ArrHour, itm.ArrMin, itm.ArrHalfMin);
                    Debug.Assert(depTime != ushort.MaxValue || arrTime != ushort.MaxValue);
                    if (depTime == ushort.MaxValue) depTime = arrTime;
                    if (arrTime == ushort.MaxValue) arrTime = depTime;
                    // Skip unkown stops (e.g. outside CR)
                    usedStops.Add(itm.StationId);
                    return new GTFS.StopTime {
                        StopId = itm.StationId.ToString(),
                        TripId = gtfsId,
                        ArrTime = arrTime,
                        DepTime = depTime,
                        Sequence = seq+1,
                    };
                }));
                //if (trainType.TrainNumber == "666/6") {
                //    Dbg(trainType.Type, trainType.TrainNumber);
                //    foreach (var stopTime in path) {
                //        Dbg(string.Format("{0,10}  {1,-32}  {2}  {3}",
                //                stopTime.StationId, stations[stopTime.StationId].Name,
                //                string.Format("{0,2}:{1:D2}", stopTime.ArrHour, stopTime.ArrMin),
                //                string.Format("{0,2}:{1:D2}", stopTime.DepHour, stopTime.DepMin)
                //                ));
                //    }
                //}
            }
            var cc = new CsvContext();
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            // Include only stations where passenger trains actually stop in the stops file.
            // This removes some false matches from stop querying.
            var gtfsStops = usedStops.Select( stationId => new GTFS.Stop {
                        Id = stationId.ToString(),
                        Name = stations[stationId].Name,
                    });
            cc.Write<GTFS.Stop>(gtfsStops, outDir + "/stops.txt", GTFS.CSVDesc);
            cc.Write<GTFS.Route>(gtfsRoutes, outDir + "/routes.txt", GTFS.CSVDesc);
            cc.Write<GTFS.Calendar>(gtfsCalendars, outDir + "/calendar.txt", GTFS.CSVDesc);
            cc.Write<GTFS.Trip>(gtfsTrips, outDir + "/trips.txt", GTFS.CSVDesc);
            cc.Write<GTFS.StopTime>(gtfsStopTimes, outDir + "/stop_times.txt", GTFS.CSVDesc);
        }


        public void Run(Arguments args) {
            this.Process(args.KangoPrefix, args.OutDir);
        }

        public static void Main(string[] args) {
            (new Kango()).Run(Arguments.Parse(args));
        }

    }
}
