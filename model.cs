using System;
using System.Collections.Generic;
using System.IO;
using MsgPack.Serialization;

namespace CCon {
    public class Model {
        public struct Stop {
            public string Name;
            public int FirstVertex; ///< The earliest at-stop vertex for the given stop.
        }
        public struct Route {
            public string ShortName;
            public ushort Calendar;
        }
        public struct Calendar {
            public DateTime start;
            public DateTime end;
            public bool[] weekdays; ///< Weekday when this service operates (characteristic vector, 0=Monday)
            // Exceptions (must be sorted), as relative number of days since `start` (start = 0).
            public short[] includes;
            public short[] excludes;
        }
        public struct Vertex {
            public ushort Stop;
            public ushort Time; ///< Compact time (seconds/5 since midnight)
            public ushort Route; ///< Route id or ushort.MaxValue if this represents standing on a stop.
            public int SuccStart;
        }

        /**
         * A packed representation of a large graph that can be easily stored to a binary file.
         */
        public class CompactGraph {
            public int NVertices;
            public int NEdges;
            public Vertex[] Vertices;
            /**
             * Packed successors of all vertices.
             *
             * The successors of vertex $u$ are stored in Succ[Verices[u].SuccStart]..Succ[Verices[u+1].SuccStart]
             */
            public int[] Succ;

            public CompactGraph() {}

            public CompactGraph(int V, int E) {
                this.NVertices = V;
                this.NEdges = E;
                this.Vertices = new Vertex[V+1];
                this.Succ = new int[E];
            }
        }

        public Stop[] Stops;
        public Route[] Routes;
        public Calendar[] Calendars;
        public CompactGraph Graph;

        public void Write(string fn) {
                //var formatter = new BinaryFormatter();
                var ser = MessagePackSerializer.Get<Model>();
                using (Stream stream = new FileStream(fn + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) {
                    ser.Pack(stream, this);
                    //formatter.Serialize(stream, this.G);
                }
                File.Replace(fn+".tmp", fn, fn+".bak");
                File.Delete(fn+".bak");
        }
        public static Model Load(string fn) {
                var ser = MessagePackSerializer.Get<Model>();
                using (Stream stream = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.None)) {
                    return ser.Unpack(stream);
                }
        }
    }
}
