using System;
using System.Collections.Generic;
using System.Linq;

using static CCon.Utils;
using static CCon.Model;

namespace CCon {
    /// A structure recording the (time) distance to a stop.
    public struct StopDistance {
        public ushort Stop;
        public ushort TimeDist; // walk time to the stop in 5s units
    }
    public class ConnectionSegment {
        public int FromVertex, ToVertex;
        public string RouteShortName;
    }
    public class Connection {
        public ushort startTime, endTime;
        public List<ConnectionSegment> segments;
    }
    public class Router {
        Model model;
        Model.Vertex[] vertices; // shortcut
        int[] succ;

        const int NotVisited = -1;
        const int StartedHere = -2;

        /// For each vertex u, pred[u] is the predecessor of $u$ on one of the
        /// paths with the latest possible departure time (or NotVisited if
        /// not yet known or StartedHere if it is the start of a path).
        int[] pred;

        public Router(Model model) {
            this.model = model;
            this.vertices = model.Graph.Vertices;
            this.succ = model.Graph.Succ;
            this.pred = new int[this.model.Graph.NVertices];
        }

        void traverse(int start) {
            if (this.pred[start] != NotVisited) return;
            this.pred[start] = StartedHere;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0) {
                int u = queue.Dequeue();
                if (this.pred[u] != NotVisited) continue; // already visited
                this.pred[u] = start;
                // Must do this convoluted thing, CompactGraph.GetSuccessors is several times slower.
                // In C, one could at least wrap this in a macro. ;-)
                for (int e = this.vertices[u].SuccStart; e < this.vertices[u+1].SuccStart; e++) {
                    // TODO check calendar
                    queue.Enqueue(this.succ[e]);
                }
            }
        }

        List< Tuple<ushort, int> > stopsVertices(ushort[] stops, bool reverse=false) {
            List< Tuple<ushort, int> > ret = new List< Tuple<ushort, int> >();
            foreach (ushort stop in stops) {
                foreach (int u in this.model.StopVertices(stop)) {
                    ret.Add( Tuple.Create(this.vertices[u].Time, u) );
                }
            }
            if (reverse) ret.Sort((x,y) => y.Item1.CompareTo(x.Item1)); // sort in reverse time order
            else ret.Sort((x,y) => x.Item1.CompareTo(y.Item1)); // sort in reverse time order
            return ret;
        }

        ConnectionSegment lastSegment(ref int v) {
            Debug.Assert(this.vertices[v].CalRoute == ushort.MaxValue);
            int end = this.pred[v];
            int u = end;
            ushort calRouteId = this.vertices[end].CalRoute;
            Debug.Assert(calRouteId != ushort.MaxValue);
            int last = end;
            while (this.vertices[u].CalRoute != ushort.MaxValue) {
                Debug.Assert(this.vertices[u].CalRoute == calRouteId);
                u = this.pred[u];
            }

        }

        public void FindConnection(ushort[] from, ushort[] to) {
            for (uint u = 0; u < this.model.Graph.NVertices; u++) this.pred[u] = NotVisited;


            foreach (var itm in this.stopsVertices(from, true)) {
                this.traverse(itm.Item2);
            }

            foreach (var itm in this.stopsVertices(to)) {
                List<ConnectionSegment> segs = new List<ConnectionSegment>();
                // Trace predecessors to reconstruct the connection.
            }
        }
    }
}
