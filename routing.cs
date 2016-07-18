using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using static CCon.Utils;
using static CCon.Model;

namespace CCon {
    /// A structure representing a place `TimeDist` time units of walking away from `Stop`.
    public struct StopDistance {
        public ushort Stop;
        public ushort TimeDist; ///< Walk time to the stop in 5s units
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
        Model.Vertex[] vertices; ///< Shortcut `this.model.Graph.Vertices`
        int[] succ; ///< Shortcut for `this.model.Graph.Succ`
        DateTime date; ///< The date for which we are looking up connections.
        int weekDay;

        /// Cache of calendar results.
        ///
        /// For each calendar IDs stores a boolean indicating whether vehicles with this
        /// calendar operate on `this.date` or not.
        Dictionary<ushort, bool> calCache = new Dictionary<ushort, bool>();

        const int NotVisited = -1;
        const int StartedHere = -2;

        /// For each vertex u, pred[u] is the predecessor of $u$ on one of the
        /// paths with the latest possible departure time (or NotVisited if
        /// not yet known or StartedHere if it is the start of a path).
        int[] pred;

        public Router(Model model, DateTime date = default(DateTime)) {
            this.model = model;
            this.vertices = model.Graph.Vertices;
            this.succ = model.Graph.Succ;
            this.pred = new int[this.model.Graph.NVertices];
            this.date = (date == default(DateTime) ? DateTime.Now.Date : date.Date);
            // Convert weekday to European convention (0=Monday)
            this.weekDay = (int)date.DayOfWeek - 1;
            if (this.weekDay == -1) this.weekDay = 6;
            Dbg("weekday:", this.weekDay);
        }

        /// Check whether the vehicle corresponding to `vertex` operates on `this.date` according to its Calendar.
        bool checkCalendar(int vertex) {
            ushort calRouteId = this.vertices[vertex].CalRoute;
            // If this vertex represents standing on a stop, that can be done on any day ;-)
            if (calRouteId == ushort.MaxValue) return true;
            ushort calendarId = this.model.CalRoutes[calRouteId].Calendar;
            return this.calCache.SetDefault(calendarId, () => {
                Calendar cal = this.model.Calendars[calendarId];
                if (cal.Excludes.Contains(date)) return false;
                else if (cal.Includes.Contains(date)) return true;
                else return (date > cal.Start && date < cal.End && cal.WeekDays[this.weekDay]);
            });
        }

        void traverse(int start) {
            if (this.pred[start] != NotVisited) return;
            if (!this.checkCalendar(start)) return;
            //Dbg("Traverse", this.model.DescribeVertex(start));
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
                    if (!this.checkCalendar(v)) continue;
                    this.pred[v] = u;
                    queue.Enqueue(v);
                }
            }
        }

        List< Tuple<ushort, int> > stopsVertices(StopDistance[] stopDists, bool subtractDist = false, bool reverse=false) {
            List< Tuple<ushort, int> > ret = new List< Tuple<ushort, int> >();
            foreach (var stopDist in stopDists) {
                int diff = (subtractDist ? -1 : +1) * stopDist.TimeDist;
                foreach (int u in this.model.StopVertices(stopDist.Stop)) {
                    if (this.vertices[u].Time+diff < 0) continue; // would overflow
                    ret.Add( Tuple.Create((ushort)(this.vertices[u].Time+diff), u) );
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
            //Dbg("lastSegment", this.model.DescribeVertex(v));
            while (true) {
                Debug.Assert(v >= 0);
                Debug.Assert(this.vertices[v].CalRoute == ushort.MaxValue);
                int end = this.pred[v];
                // Skip any waiting on stop
                while (end >= 0 && this.vertices[end].CalRoute == ushort.MaxValue) end = this.pred[end];
                if (end == StartedHere) { return null; } // hit end of path

                int u = end;
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

        public List<Connection> FindConnections(StopDistance[] from, StopDistance[] to) {
            List<Connection> ret = new List<Connection>();
            for (uint u = 0; u < this.model.Graph.NVertices; u++) this.pred[u] = NotVisited;
            // In case of virtual stops, store the virtual start/end time (i.e., when we would
            // stand at the virtual stop).
            Dictionary<int, ushort> virtualStart = new Dictionary<int, ushort>();

            foreach (var itm in this.stopsVertices(from, subtractDist: true, reverse: true)) {
                virtualStart[itm.Item2] = itm.Item1;
                this.traverse(itm.Item2);
            }

            foreach (var itm in this.stopsVertices(to, subtractDist: false)) {
                if (this.pred[itm.Item2] == NotVisited) continue; // cannot get here this soon!
                // Don't connections that have waiting on stop at the end (there will be another
                // connection with an earlier arrival time that shall be listed instead).
                if (this.vertices[this.pred[itm.Item2]].CalRoute == ushort.MaxValue) continue;
                //dumpPath(itm.Item2);
                Connection conn = new Connection();
                // Trace predecessors to reconstruct the connection.
                int v = itm.Item2;
                ConnectionSegment seg;
                while ((seg = this.lastSegment(ref v)) != null) conn.Segments.Add(seg);
                conn.Segments.Reverse(); // we found segments from last to first, correct that
                //Dbg("# Conn with",conn.Segments.Count, "segments", this.model.DescribeVertex(itm.Item2));
                //foreach (var tmpseg in conn.Segments) {
                //    Dbg("Segment",this.model.DescribeVertex(tmpseg.Start), "..", this.model.DescribeVertex(tmpseg.End));
                //}
                // If from/to is a virtual stop, use virtual stop time as StartTime/EndTime.
                conn.StartTime = virtualStart[v];
                conn.EndTime = (ushort) (itm.Item1 - TransferTime);

                ret.Add(conn);
            }
            return ret;
        }
    }
}
