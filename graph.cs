using static CCon.GTFS;
using System.Collections.Generic;
namespace CCon {
    public class Vertex {
        public Stop Stop;
        public Trip Trip;
        public int Time;

        public List<Vertex> Succ = new List<Vertex>();

    }
    public class Graph {

    }

}
