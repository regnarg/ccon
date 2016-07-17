using System;
using System.Collections.Generic;
using System.Linq;
using static CCon.GTFS;
using static CCon.Utils;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using VertexKey = System.Tuple<CCon.GTFS.Stop, ushort, CCon.GTFS.Trip>;
using CalRouteKey = System.Tuple<CCon.GTFS.Calendar, CCon.GTFS.Route>;

namespace CCon {
    class ModelBuilder {
        public ushort TransferTime = (ushort) (2*60/TimeGranularity);
        GTFS gtfs;
        //Dictionary< VertexKey, Vertex > vertices = new Dictionary< VertexKey, Vertex >();
        /// For each stop the set of all event (departure/arrival) times.
        Dictionary< Stop, SortedSet<ushort> > stopEventTimes = new Dictionary< Stop, SortedSet<ushort> >();

        CompactTableBuilder<Stop,        Model.Stop    > stopBuilder;
        CompactTableBuilder<CalRouteKey, Model.CalRoute> calRouteBuilder;
        CompactTableBuilder<VertexKey,   Model.Vertex  > vertexBuilder;
        List< List<int> > succBuilder = new List< List<int> >();

        public ModelBuilder(GTFS gtfs) {
            this.gtfs = gtfs;
            this.stopBuilder     = new CompactTableBuilder<Stop,        Model.Stop    >(this.buildStop);
            this.calRouteBuilder = new CompactTableBuilder<CalRouteKey, Model.CalRoute>(this.buildCalRoute);
            this.vertexBuilder   = new CompactTableBuilder<VertexKey,   Model.Vertex  >(this.buildVertex);
            this.stopBuilder.Add(gtfs.Stops.Values);
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
        Model.CalRoute buildCalRoute(CalRouteKey key) {
            return new Model.CalRoute {
                RouteShortName = key.Item2.ShortName,
                // TODO
            };
        }

        int getVert(Stop stop, ushort time, Trip trip) {
            return this.vertexBuilder.Add(new VertexKey(stop, time, trip));
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

        public Model BuildModel() {
            using (new Profiler("Create graph"))
                this.CreateGraph();
            var model = new Model();
            using (new Profiler("Build compact graph"))
                model.Graph = this.BuildCompactGraph();
            using (new Profiler("Build compact tables")) {
                model.Stops = this.stopBuilder.BuildTable();
                model.CalRoutes = this.calRouteBuilder.BuildTable();
            }
            //PyREPL("model", model, "gtfs", gtfs, "Utils", typeof(Utils), "builder", this);
            return model;
        }

        public static void Main(string [] args) {
            var gtfs = new GTFS();
            gtfs.Load(args[0]);
            var builder = new ModelBuilder(gtfs);
            var model = builder.BuildModel();
            model.Write(args[1]);
        }
    }
}
