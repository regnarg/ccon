using System;
using System.Collections.Generic;
using static CCon.GTFS;
using static CCon.Utils;
using QuickGraph;
using QuickGraph.Algorithms.Search;
using QuickGraph.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace CCon {
    using VertexKey = Tuple<Stop, int, Trip>;
    using CEdge = Edge<Tuple<Stop, int, Trip> >;
    using Graph = BidirectionalGraph< Tuple<Stop, int, Trip>, Edge<Tuple<Stop, int, Trip> > >;
    class GTFS2Graph {
        public int TransferTime = 2*60;
        GTFS gtfs;
        //Dictionary< VertexKey, Vertex > vertices = new Dictionary< VertexKey, Vertex >();
        /// For each stop the set of all event (departure/arrival) times.
        Dictionary< Stop, SortedSet<int> > stopEventTimes = new Dictionary< Stop, SortedSet<int> >();
        public GTFS2Graph(GTFS gtfs) {
            this.gtfs = gtfs;
        }
        //Vertex getVertex(Stop stop, int time, Trip trip) {
        //    return this.vertices.SetDefault(new VertexKey(stop, time, trip),
        //            ()=> new Vertex{ Stop=stop, Time=time, Trip=trip });
        //}
        Graph G = new Graph();

        public void AddEdge(VertexKey u, VertexKey v) {
            G.AddVerticesAndEdge(new Edge<VertexKey>(u,v));
        }

        public Graph CreateGraph() {
            Console.Error.WriteLine("begin");
            foreach (var trip in gtfs.Trips.Values) {
                foreach (var stopTime in trip.StopTimes) {
                    // Boarding edge
                    AddEdge(new VertexKey(stopTime.Stop, stopTime.DepTime, null),
                            new VertexKey(stopTime.Stop, stopTime.DepTime, trip));
                    // Getting-off edge
                    AddEdge(new VertexKey(stopTime.Stop, stopTime.ArrTime,              trip),
                            new VertexKey(stopTime.Stop, stopTime.ArrTime+TransferTime, null));
                    // Stay-in-vehicle edge
                    AddEdge(new VertexKey(stopTime.Stop, stopTime.ArrTime, trip),
                            new VertexKey(stopTime.Stop, stopTime.DepTime, trip));
                    stopEventTimes.SetDefault(stopTime.Stop, ()=>new SortedSet<int>());
                    stopEventTimes[stopTime.Stop].Add(stopTime.DepTime);
                    stopEventTimes[stopTime.Stop].Add(stopTime.ArrTime+TransferTime);
                }
                foreach (var pair in trip.StopTimes.Pairs()) {
                    // Travel-to-the-next-stop edge
                    AddEdge(new VertexKey(pair.Item1.Stop, pair.Item1.DepTime, trip),
                              new VertexKey(pair.Item2.Stop, pair.Item2.ArrTime, trip));
                }
            }
            foreach (var itm in this.stopEventTimes) {
                foreach (var pair in itm.Value.Pairs()) {
                    // A "wait on stop for the next event" edge.
                    AddEdge(new VertexKey(itm.Key, pair.Item1, null),
                              new VertexKey(itm.Key, pair.Item2, null));
                }
            }
            Console.Error.WriteLine("end");
			//PyREPL("G", G);
            return G;
        }
		public void WriteGraph(string fn) {
			//var formatter = new BinaryFormatter();
			//formatter.Serialize(stream, this.G);
			using (Stream stream = new FileStream(fn, FileMode.Create, FileAccess.Write, FileShare.None)) {
				G.SerializeToBinary(stream);
			}
		}
        public static void Main(string [] args) {
            var gtfs = new GTFS();
            gtfs.Load(args[0]);
			var creator = new GTFS2Graph(gtfs);
            creator.CreateGraph();
			creator.WriteGraph("out.bin");
        }
    }
}
