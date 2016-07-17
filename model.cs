using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using MsgPack.Serialization;
using static CCon.Utils;

namespace CCon {
    public class Model {
        public struct Stop {
            public string GTFSId;
            public string Name;
            public int FirstVertex; ///< The earliest at-stop vertex for the given stop.
        }
        public struct Route {
            public string ShortName;
        }
        public struct CalRoute {
            public string RouteShortName; ///< The line number of the route (e.g. "12", "C")
            public DateTime start; ///< First day vehicles with this calendar operate (midnight)
            public DateTime end; ///< The first day _after_ vehicles with this calendar stop operating (midnight)
            public bool[] weekdays; ///< Weekday when this service operates (characteristic vector, 0=Monday)
            /// List of days when vehicles with this calendar exceptionally do not operate,
            /// even though they are in the [start, end) range.
            public short[] excludes;
            public short[] includes; ///< List of 
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vertex {
            public ushort Stop;
            public ushort Time; ///< Compact time (seconds/5 since midnight)
            public ushort CalRoute; ///< Id of a CalRoute structure that holds combined calendar
                                    ///  and route info (to save one ushort per vertex).
            public int SuccStart;
        }

        /**
         * A packed representation of a large graph that can be easily stored to a binary file.
         */
        public class CompactGraph {
            public int NVertices;
            public int NEdges;
            [MessagePackIgnore] // Saved separately, see WriteData/LoadData
            public Vertex[] Vertices;
            /**
             * Packed successors of all vertices.
             *
             * The successors of vertex $u$ are stored in Succ[Verices[u].SuccStart]..Succ[Verices[u+1].SuccStart]
             */
            [MessagePackIgnore] // Saved separately, see WriteData/LoadData
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

            public IEnumerable<int> GetSuccessors(int u) {
                int start = this.Vertices[u].SuccStart;
                int end = this.Vertices[u+1].SuccStart;
                return new ArraySegment<int>(this.Succ, start, end-start);
            }
            
            internal void WriteData(string fn) {
                Console.Error.WriteLine(string.Format("Vertex size: {0}", Marshal.SizeOf(typeof(Vertex))));
                int verticesSize = this.Vertices.Length * Marshal.SizeOf(typeof(Vertex));
                int edgesSize = this.Succ.Length * Marshal.SizeOf(typeof(int));
                int size = verticesSize + edgesSize;
                File.Delete(fn);
                var mmf = MemoryMappedFile.CreateFromFile(fn, FileMode.Create, "x", size, MemoryMappedFileAccess.ReadWrite);
                var acc = mmf.CreateViewAccessor();
                using (new Profiler("Write Graph")) {
                    int pos = 0;
                    acc.WriteArray(pos, this.Vertices, 0, this.Vertices.Length);
                    pos += verticesSize;
                    acc.WriteArray(pos, this.Succ, 0, this.Succ.Length);
                    acc.Flush();
                }
                acc.Dispose();
                mmf.Dispose();
            }

            internal void  LoadData(string fn) {
                Console.Error.WriteLine(string.Format("Vertex size: {0}", Marshal.SizeOf(typeof(Vertex))));
                int V=this.NVertices, E=this.NEdges;
                using (new Profiler("allocate arrays")) {
                    this.Vertices = new Vertex[V+1];
                    this.Vertices[V].SuccStart = E;
                    this.Succ = new int[E];
                }
                int verticesSize = this.Vertices.Length * Marshal.SizeOf(typeof(Vertex));
                //int edgesSize = this.Succ.Length * Marshal.SizeOf(typeof(int));
                var mmf = MemoryMappedFile.CreateFromFile(fn, FileMode.Open, "x", 0, MemoryMappedFileAccess.Read);
                var acc = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                using (new Profiler("Read graph")) {
                    int pos = 0;
                    acc.ReadArray(pos, this.Vertices, 0, this.Vertices.Length);
                    pos += verticesSize;
                    acc.ReadArray(pos, this.Succ, 0, this.Succ.Length);
                }
                acc.Dispose();
                mmf.Dispose();
            }
        }

        public Stop[] Stops;
        public CalRoute[] CalRoutes;
        public CompactGraph Graph;

        public void Write(string fn) {
            //var formatter = new BinaryFormatter();
            var ser = MessagePackSerializer.Get<Model>();
            using (var stream = new FileStream(fn + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) {
                using (new Profiler("Write MsgPack"))
                    ser.Pack(stream, this);
            }
            this.Graph.WriteData(fn+".mmap.tmp");

            // An "almost-atomic" replace of the two files. Very unlikely to fail in the middle of this.
            File.Delete(fn);
            File.Delete(fn+".mmap");
            File.Move(fn+".tmp", fn);
            File.Move(fn+".mmap.tmp", fn+".mmap");
        }
        public static Model Load(string fn) {
            var ser = MessagePackSerializer.Get<Model>();
            Model model;
            using (Stream stream = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.None)) {
                using (new Profiler("Load MsgPack"))
                    model = ser.Unpack(stream);
                using (new Profiler("Load graph"))
                    model.Graph.LoadData(fn+".mmap");
            }
            return model;
        }
    }
}
