using System;
using System.Collections.Generic;
using static CCon.GTFS;
using static CCon.Utils;

namespace CCon {
    using VertexKey = Tuple<Stop, int, Trip>;
    class GTFS2Graph {
        public int TransferTime = 2*60;
        GTFS gtfs;
        Dictionary< VertexKey, Vertex > vertices = new Dictionary< VertexKey, Vertex >();
        /// For each stop the set of all event (departure/arrival) times.
        Dictionary< Stop, SortedSet<int> > stopEventTimes;
        public GTFS2Graph(GTFS gtfs) {
            this.gtfs = gtfs;
        }
        Vertex getVertex(Stop stop, int time, Trip trip) {
            return this.vertices.SetDefault(new VertexKey(stop, time, trip),
                    ()=> new Vertex{ Stop=stop, Time=time, Trip=trip });
        }
        public Graph CreateGraph() {
            foreach (var trip in gtfs.Trips.Values) {
                foreach (var stopTime in trip.StopTimes) {
                    // Boarding edge
                    getVertex(stopTime.Stop, stopTime.DepTime, null).Succ.Add(
                        getVertex(stopTime.Stop, stopTime.DepTime, trip));
                    // Getting-off edge
                    getVertex(stopTime.Stop, stopTime.ArrTime, trip).Succ.Add(
                        getVertex(stopTime.Stop, stopTime.ArrTime+TransferTime, null));
                }
            }
            return null;
        }
        public static void Main(string [] args) {
            var gtfs = new GTFS();
            gtfs.Load(args[0]);
            var G = (new GTFS2Graph(gtfs)).CreateGraph();
        }
    }
}
