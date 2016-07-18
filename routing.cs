using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using static CCon.Utils;
using static CCon.Model;

namespace CCon {
    /// A structure recording the (time) distance to a stop.
    public struct StopDistance {
        public ushort Stop;
        public ushort TimeDist; // walk time to the stop in 5s units
    }
    public class ConnectionSegment {
        public int Start, End; ///< In-vehicle vertices for the start and end of the ride.
        public ConnectionSegment(int start, int end) {
            this.Start = start;
            this.End = end;
        }
    }
    public class Connection {
        public ushort StartTime, EndTime;
        public List<ConnectionSegment> Segments = new List<ConnectionSegment>();
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
            Dbg("Traverse", this.model.DescribeVertex(start));
            this.pred[start] = StartedHere;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0) {
                int u = queue.Dequeue();
                //Dbg("Visit", this.model.DescribeVertex(u));
                // Must do this convoluted thing, CompactGraph.GetSuccessors is several times slower.
                // In C, one could at least wrap this in a macro. ;-)
                for (int e = this.vertices[u].SuccStart; e < this.vertices[u+1].SuccStart; e++) {
                    // TODO check calendar
                    int v = this.succ[e];
                    if (this.pred[v] != NotVisited) continue;
                    this.pred[v] = u;
                    queue.Enqueue(v);
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

        /// Cut off the last segment (i.e. travel by one vehicle) from the path ending in $v$.
        ///
        /// Return the found segment and update $v$ in place to be the endpoint of the remaining path.
        ConnectionSegment lastSegment(ref int v) {
            Dbg("lastSegment", this.model.DescribeVertex(v));
            while (true) {
                Debug.Assert(v >= 0);
                Debug.Assert(this.vertices[v].CalRoute == ushort.MaxValue);
                int end = this.pred[v];
                // Skip any waiting on stop
                while (end >= 0 && this.vertices[end].CalRoute == ushort.MaxValue) end = this.pred[end];
                if (end == StartedHere) { v = -1; return null; } // hit end of path

                int u = end;
                Dbg("end",end);
                ushort calRouteId = this.vertices[end].CalRoute;
                Debug.Assert(calRouteId != ushort.MaxValue);
                int start = end;
                while (this.vertices[u].CalRoute != ushort.MaxValue) {
                    Debug.Assert(this.vertices[u].CalRoute == calRouteId);
                    start = u;
                    u = this.pred[u];
                }
                v = u;
                // It's perfectly valid for the search algorithm to find a path that gets onto
                // a vehicle and immediately gets off again without riding anywhere. It is as
                // fast as standing on a stop, mind you. But we do not want to report such
                // zero-length segments to the user.
                if (start == end) continue;
                else return new ConnectionSegment(start, end);
            }
        }

        void dumpPath(int end) {
            List<int> path = new List<int>();
            int u = end;
            while (u > 0) {
                path.Add(u);
                u = this.pred[u];
            }
            path.Reverse();
            Dbg("path:");
            foreach (var v in path) Dbg("  "+this.model.DescribeVertex(v));
        }

        public List<Connection> FindConnections(ushort[] from, ushort[] to) {
            List<Connection> ret = new List<Connection>();
            for (uint u = 0; u < this.model.Graph.NVertices; u++) this.pred[u] = NotVisited;

            foreach (var itm in this.stopsVertices(from, true)) {
                this.traverse(itm.Item2);
            }

            foreach (var itm in this.stopsVertices(to)) {
                if (this.pred[itm.Item2] == NotVisited) continue; // cannot get here this soon!
                // Don't connections that have waiting on stop at the end (there will be another
                // connection with an earlier arrival time that shall be listed instead).
                if (this.vertices[this.pred[itm.Item2]].CalRoute == ushort.MaxValue) continue;
                dumpPath(itm.Item2);
                Connection conn = new Connection();
                // Trace predecessors to reconstruct the connection.
                int v = itm.Item2;
                ConnectionSegment seg;
                while ((seg = this.lastSegment(ref v)) != null) conn.Segments.Add(seg);
                conn.Segments.Reverse(); // we found segments from last to first, correct that
                Dbg("# Conn with",conn.Segments.Count, "segments", this.model.DescribeVertex(itm.Item2));
                foreach (var tmpseg in conn.Segments) {
                    Dbg("Segment",this.model.DescribeVertex(tmpseg.Start), "..", this.model.DescribeVertex(tmpseg.End));
                }
                conn.StartTime = this.vertices[conn.Segments[0].Start].Time;
                conn.EndTime = this.vertices[conn.Segments[conn.Segments.Count - 1].Start].Time;
                ret.Add(conn);
            }
            return ret;
        }
    }
}
