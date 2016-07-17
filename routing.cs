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

        public void Traverse(int start) {
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
                    queue.Enqueue(this.succ[e]);
                }
            }
        }

        public void FindConnection(ushort[] from, ushort[] to) {
            for (uint u = 0; u < this.model.Graph.NVertices; u++) this.pred[u] = NotVisited;
            List< Tuple<ushort, int> > departVertices = new List< Tuple<ushort, int> >();
            foreach (ushort stop in from) {
                foreach (int u in this.model.StopVertices(stop)) {
                    departVertices.Add( Tuple.Create(this.vertices[u].Time, u) );
                }
            }
            departVertices.Sort((x,y) => y.Item1.CompareTo(x.Item1)); // sort in reverse time order
            foreach (var itm in departVertices) {
                this.Traverse(itm.Item2);
            }
        }
    }
}
