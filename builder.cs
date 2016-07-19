using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using static CCon.GTFS;
using static CCon.Utils;

using ProjNet;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

using CommandLine;
using CommandLine.Text;

using VertexKey = System.Tuple<CCon.GTFS.Stop, ushort, CCon.GTFS.Trip>;
using CalRouteKey = System.Tuple<CCon.GTFS.Calendar, CCon.GTFS.Route>;
//
//HACK to get this into help message
[assembly:AssemblyCopyright("Usage: ccon-build GTFS-DIR [-o FILE]")]
[assembly:AssemblyVersion("0.1")]

namespace CCon {
    /// A class that for a given set of points, finds all pairs closer than maxDistance.
    ///
    /// It uses the Manhattan metric because it gives a better approximation of walking
    /// distances in cities than the Euclidean metric.
    class ClosePointFinder<T> {
        Dictionary< Tuple<int, int>, List< Tuple<double,double,T> > > cells
            = new Dictionary< Tuple<int, int>, List< Tuple<double,double,T> > >();
        double maxDistance;
        public ClosePointFinder(double maxDistance) {
            this.maxDistance = maxDistance;
        }
        Tuple<int, int> cell(double x, double y) {
            return Tuple.Create((int)(x/maxDistance), (int)(y/maxDistance));
        }
        public void Add(double x, double y, T obj) {
            this.cells.SetDefault(this.cell(x,y), ()=>new List< Tuple<double,double,T> >()).Add(Tuple.Create(x,y,obj));
        }
        /// Return the distance of two item's coordinated in the Manhattan metric.
        double distance(Tuple<double,double,T> a, Tuple<double,double,T> b) {
            return Math.Abs(a.Item1 - b.Item1) + Math.Abs(a.Item2 - b.Item2);
        }
        /// Return all pairs of points closer than `maxDistance`.
        ///
        /// The pairs are oriented, i.e. of points A and B are close, both
        /// (A,B) and (B,A) pairs are returned.
        public IEnumerable< Tuple<T,T,double> > ClosePairs() {
            foreach (var cellItem in this.cells) {
                var cell = cellItem.Key;
                foreach (var itm in cellItem.Value) {
                    // Look for close neighbours in this cell and all the 8 adjacent cells.
                    for (int dx = -1;  dx <= 1; dx++)
                    for (int dy = -1;  dy <= 1; dy++) {
                        var ncell = Tuple.Create(cell.Item1+dx, cell.Item2+dy);
                        if (!this.cells.ContainsKey(ncell)) continue;
                        foreach (var neigh in this.cells[ncell]) {
                            if (neigh.Item3.Equals(itm.Item3)) continue; // do not output any point as close to itself
                            double dist = this.distance(itm, neigh);
                            if (dist < this.maxDistance)
                                yield return Tuple.Create(itm.Item3, neigh.Item3, dist);
                        }
                    }
                }
            }
        }
    }
    class ModelBuilder {
        public class Arguments {
            [Value(0, MetaValue="GTFS-DIR", HelpText="The directory with (unzipped) GTFS feed", Required=true)]
            public string GTFSDir { get; set; }

            [Option('o', "output", MetaValue="FILE", HelpText="Output file [default: ~/.cache/ccon.dat]")]
            public string Output { get; set; }

            public static Arguments Parse(string[] args) {
                var res =  Parser.Default.ParseArguments<Arguments>(args) as Parsed<Arguments>;
                if (res == null) {
                    Environment.Exit(1);
                }
                return res.Value;
            }
        };

        public double MaxWalkDistance = 250; ///< The maximum distance for walks between stops [m].
        // Humans usually walk faster but we have to account for street crossings and similar.
        public double WalkSpeed = 4.5; ///< Assumed walking speed [km/h].
        GTFS gtfs;
        //Dictionary< VertexKey, Vertex > vertices = new Dictionary< VertexKey, Vertex >();
        /// For each stop the set of all event (departure/arrival) times.
        Dictionary< Stop, SortedSet<ushort> > stopEventTimes = new Dictionary< Stop, SortedSet<ushort> >();

        CompactTableBuilder<Stop,        Model.Stop    > stopBuilder;
        CompactTableBuilder<Calendar,    Model.Calendar> calendarBuilder;
        CompactTableBuilder<CalRouteKey, Model.CalRoute> calRouteBuilder;
        CompactTableBuilder<VertexKey,   Model.Vertex  > vertexBuilder;
        List< List<int> > succBuilder = new List< List<int> >();

        public ModelBuilder(GTFS gtfs) {
            this.gtfs = gtfs;
            this.stopBuilder     = new CompactTableBuilder<Stop,        Model.Stop    >(this.buildStop);
            this.calendarBuilder = new CompactTableBuilder<Calendar,    Model.Calendar>(this.buildCalendar);
            this.calRouteBuilder = new CompactTableBuilder<CalRouteKey, Model.CalRoute>(this.buildCalRoute);
            this.vertexBuilder   = new CompactTableBuilder<VertexKey,   Model.Vertex  >(this.buildVertex);
            this.stopBuilder.Add(gtfs.Stops.Values);
            this.calendarBuilder.Add(gtfs.Calendars.Values);
            foreach (var trip in gtfs.Trips.Values) {
                this.calRouteBuilder.Add(new CalRouteKey(trip.Calendar, trip.Route));
            }
        }

        void AddEdge(int uId, int vId) {
            while (this.succBuilder.Count-1 < uId) { this.succBuilder.Add(new List<int>()); }
            //this.succBuilder.SetDefault(uId, ()=>new List<int>()).Add(vId);
            this.succBuilder[(int)uId].Add(vId);
        }

        Model.Vertex buildVertex(VertexKey key) {
            return new Model.Vertex {
                Stop = (key.Item1 == null ? ushort.MaxValue : (ushort)this.stopBuilder.GetId(key.Item1)),
                Time = (ushort) key.Item2,
                CalRoute = (key.Item3 == null ? ushort.MaxValue : (ushort)this.calRouteBuilder.GetId(
                                                new CalRouteKey(key.Item3.Calendar, key.Item3.Route))),
            };
        }
        Model.Stop buildStop(Stop stop) {
            int firstVertex = -1;
            ushort firstTime = ushort.MaxValue;
            try {
                firstTime = this.stopEventTimes[stop].First();
            } catch (KeyNotFoundException) { }
            if (firstTime != ushort.MaxValue) {
                firstVertex = this.vertexBuilder.GetId(new VertexKey(stop, firstTime, null));
            }
            return new Model.Stop { GTFSId = stop.Id, Name = stop.Name, FirstVertex = firstVertex };
        }
        Model.Calendar buildCalendar(Calendar cal) {
            return new Model.Calendar {
                WeekDays = new bool[7] { cal.Monday!=0, cal.Tuesday!=0, cal.Wednesday!=0,
                                         cal.Thursday!=0, cal.Friday!=0, cal.Saturday!=0, cal.Sunday!=0 },
                Start = cal.Start,
                End = cal.End,
                Excludes = cal.Excludes,
                Includes = cal.Includes,
            };
        }
        Model.CalRoute buildCalRoute(CalRouteKey key) {
            return new Model.CalRoute {
                RouteShortName = key.Item2.ShortName,
                Calendar = (ushort) this.calendarBuilder.GetId(key.Item1),
            };
        }

        int getVert(Stop stop, ushort time, Trip trip) {
            return this.vertexBuilder.Add(new VertexKey(stop, time, trip));
        }

        IEnumerable< Tuple<Stop, Stop, double> > findCloseStops() {
            // Add walk edges.
            ClosePointFinder<Stop> cpf = new ClosePointFinder<Stop>(MaxWalkDistance);

            CoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;
            CoordinateSystem utm33 = ProjectedCoordinateSystem.WGS84_UTM(33, true);
            var fact = new CoordinateTransformationFactory();
            var transformation = fact.CreateFromCoordinateSystems(wgs84, utm33);

            foreach (var stop in this.gtfs.Stops.Values) {
                if (stop.Lat == 0 && stop.Lon == 0) continue; // coords not known
                // Convert coordinates to UTM (local Cartesian metre grid) to correctly
                // compute distances.
                double[] utm = transformation.MathTransform.Transform(new double[] { stop.Lon, stop.Lat });
                cpf.Add(utm[0], utm[1], stop);
            }

            return cpf.ClosePairs();
        }

        void addWalkEdges() {
            foreach (var pair in this.findCloseStops()) {
                if (!(stopEventTimes.ContainsKey(pair.Item1) && stopEventTimes.ContainsKey(pair.Item2)))
                    continue;
                Dbg("Close pair", pair.Item1.Name, "..", pair.Item2.Name, pair.Item3);

                ushort walkTime = (ushort) Math.Round(pair.Item3 / (this.WalkSpeed / 3.6) / TimeGranularity);

                var trgTimes = stopEventTimes[pair.Item2].ToList();
                foreach (ushort tm1 in stopEventTimes[pair.Item1]) {
                    ushort arrTime = (ushort)(tm1 + walkTime);
                    int trgIdx = trgTimes.BinarySearch(arrTime);   
                    if (trgIdx < 0) trgIdx = ~trgIdx; // exact time not found -> get first greater
                    if (trgIdx >= trgTimes.Count) continue;
                    while (trgIdx > 0 && trgTimes[trgIdx-1] >= arrTime) trgIdx--;
                    AddEdge(getVert(pair.Item1, tm1, null),
                            getVert(pair.Item2, trgTimes[trgIdx], null));
                }
                
            }
        }

        void CreateGraph() {
            foreach (var trip in gtfs.Trips.Values) {
                foreach (var stopTime in trip.StopTimes) {
                    // Boarding edge
                    AddEdge(getVert(stopTime.Stop, stopTime.DepTime, null),
                            getVert(stopTime.Stop, stopTime.DepTime, trip));
                    // Getting-off edge
                    AddEdge(getVert(stopTime.Stop, stopTime.ArrTime,              trip),
                            getVert(stopTime.Stop, (ushort)(stopTime.ArrTime+TransferTime), null));
                    // Stay-in-vehicle edge
                    AddEdge(getVert(stopTime.Stop, stopTime.ArrTime, trip),
                            getVert(stopTime.Stop, stopTime.DepTime, trip));
                    stopEventTimes.SetDefault(stopTime.Stop, ()=>new SortedSet<ushort>());
                    stopEventTimes[stopTime.Stop].Add(stopTime.DepTime);
                    stopEventTimes[stopTime.Stop].Add((ushort)(stopTime.ArrTime+TransferTime));
                }
                foreach (var pair in trip.StopTimes.Pairs()) {
                    // Travel-to-the-next-stop edge
                    AddEdge(getVert(pair.Item1.Stop, pair.Item1.DepTime, trip),
                            getVert(pair.Item2.Stop, pair.Item2.ArrTime, trip));
                }
            }
            using (new Profiler("Add walk edges"))
                this.addWalkEdges();
            // IMPORTANT: Wait edges must be added last. The router counts on the wait edge
            //            being the last in the successor array for any vertex.
            foreach (var itm in this.stopEventTimes) {
                foreach (var pair in itm.Value.Pairs()) {
                    // A "wait on stop for the next event" edge.
                    AddEdge(getVert(itm.Key, pair.Item1, null),
                            getVert(itm.Key, pair.Item2, null));
                }
            }
            //PyREPL("G", G);
        }

        Model.CompactGraph BuildCompactGraph() {
            int V = this.vertexBuilder.Count;
            int E = 0;
            for (int u = 0; u < V; u++) E += this.succBuilder[u].Count;
            var G = new Model.CompactGraph(V, E);
            Array.Copy(this.vertexBuilder.BuildTable(), 0, G.Vertices, 0, V);
            int curEdge = 0;
            for (int u = 0; u < V; u++) {
                G.Vertices[u].SuccStart = curEdge;
                foreach(var target in this.succBuilder[u]) {
                    G.Succ[curEdge++] = target;
                }
            }
            return G;
        }
        
        void BuildCompletions(Model model, string fn) {
            using (var sw = new StreamWriter(fn)) {
                foreach (var stop in model.Stops) {
                    // Make completions with dashes to spare the user from quoting.
                    var name = NormalizeStopName(stop.Name).Replace(" ", "-");
                    sw.WriteLine(name);
                }
            }
        }

        public Model BuildModel() {
            using (new Profiler("Create graph"))
                this.CreateGraph();
            var model = new Model();
            using (new Profiler("Build compact graph"))
                model.Graph = this.BuildCompactGraph();
            using (new Profiler("Build compact tables")) {
                model.Stops = this.stopBuilder.BuildTable();
                model.CalRoutes = this.calRouteBuilder.BuildTable();
                model.Calendars = this.calendarBuilder.BuildTable();
            }
            //PyREPL("model", model, "gtfs", gtfs, "Utils", typeof(Utils), "builder", this);
            return model;
        }

        public static void Main(string [] args) {
            var pargs = Arguments.Parse(args);
            var gtfs = new GTFS();
            gtfs.Load(pargs.GTFSDir);
            var builder = new ModelBuilder(gtfs);
            var model = builder.BuildModel();
            var output = pargs.Output ?? GetDefaultDbPath();
            model.Write(output);
            builder.BuildCompletions(model, output+".comp");
        }
    }
}
