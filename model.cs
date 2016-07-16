using System;
using System.Collections.Generic;
using System.IO;
using MsgPack.Serialization;
using static CCon.Utils;

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

            public void Write(BinaryWriter wr) {
                wr.Write(Stop);
                wr.Write(Time);
                wr.Write(Route);
                wr.WriteUInt24((uint)SuccStart);
            }

            public void Load(BinaryReader rd) {
                this.Stop = rd.ReadUInt16();
                this.Time = rd.ReadUInt16();
                this.Route = rd.ReadUInt16();
                this.SuccStart = (int)rd.ReadUInt24();
            }
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
                // Sentinel to make the Succ[Verices[u].SuccStart]..Succ[Verices[u+1].SuccStart] range
                // valid even for the last vertex.
                this.Vertices[V].SuccStart = E;
                this.Succ = new int[E];
            }
            
            public void Write(BinaryWriter wr) {
                wr.Write(this.NVertices);
                wr.Write(this.NEdges);
                foreach (var vert in this.Vertices) vert.Write(wr);
                foreach (int succ in this.Succ) wr.WriteUInt24((uint) succ);
            }

            public static CompactGraph Load(BinaryReader rd) {
                int V = rd.ReadInt32();
                int E = rd.ReadInt32();
                var G = new CompactGraph(V,E);
                for (int u = 0; u < V; u++) {
                    G.Vertices[u].Load(rd);
                }
                return G;
            }
        }

        public Stop[] Stops;
        public Route[] Routes;
        public Calendar[] Calendars;
        [MessagePackIgnore]
        public CompactGraph Graph;

        public void Write(string fn) {
                //var formatter = new BinaryFormatter();
                var ser = MessagePackSerializer.Get<Model>();
                using (Stream stream = new FileStream(fn + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) {
                    using (new Profiler("Write MsgPack"))
                        ser.Pack(stream, this);
                    using (new Profiler("Write Graph"))
                        this.Graph.Write(new BinaryWriter(stream));
                    //formatter.Serialize(stream, this.G);
                }

                // Not atomic. Win32 filesystem semantics (imported into .NET even on Mono) suck.
                File.Delete(fn);
                File.Move(fn+".tmp", fn);
        }
        public static Model Load(string fn) {
                var ser = MessagePackSerializer.Get<Model>();
                Model model;
                using (Stream stream = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.None)) {
                    using (new Profiler("Load MsgPack"))
                        model = ser.Unpack(stream);
                    using (new Profiler("Load graph"))
                        model.Graph = CompactGraph.Load(new BinaryReader(stream));
                }
                return model;
        }
    }
}
