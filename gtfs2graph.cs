using System;
using System.Yaml.Serialization;

namespace CCon {
    class GTFS2Graph {
        public static void Main(string [] args) {
            var G = new GTFS(args[0]);
            Console.WriteLine((new YamlSerializer()).Serialize(G));
        }
    }
}
