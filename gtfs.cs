using System;
using LINQtoCSV;
using System.Collections.Generic;
using System.Linq;

namespace CCon {
    public class GTFS {
        public class Invalid : Exception {
            public Invalid(string msg) : base("Invalid GTFS: " + msg) {}
        }
        public class Stop {
            [CsvColumn(Name="stop_id")]
            public string Id;
            [CsvColumn(Name="stop_name")]
            public string Name;
            [CsvColumn(Name="stop_lat")]
            public double Lat;
            [CsvColumn(Name="stop_lon")]
            public double Lon;
        }
        public class Route {
            [CsvColumn(Name="route_id")]
            public string Id;
            [CsvColumn(Name="route_short_name")]
            public string ShortName;
        }
        public class Service {
            [CsvColumn(Name="service_id")]
            public string Id;
            [CsvColumn(Name="route_short_name")]
            public string ShortName;
        }
        public class StopTime {
            [CsvColumn(Name="trip_id")]
            public string TripId;
            [CsvColumn(Name="arrival_time")]
            public TimeSpan ArrTime;
            [CsvColumn(Name="departure_time")]
            public TimeSpan DepTime;
            [CsvColumn(Name="stop_id")]
            public string StopId;
            public Stop Stop;
            [CsvColumn(Name="sequence")]
            public uint Sequence;
        }
        public class Trip {
            [CsvColumn(Name="trip_id")]
            public string Id;
            [CsvColumn(Name="route_id")]
            public string RouteId;
            public Route Route;
            [CsvColumn(Name="service_id")]
            public string ServiceId;
            public Service Service;

            public StopTime[] StopTimes;
        }

        string dir;
        public Dictionary<string, Stop> Stops;
        public Dictionary<string, Route> Routes;
        public Dictionary<string, Service> Services; 
        public Dictionary<string, Trip> Trips; 

        public GTFS(string dir) {
            this.dir = dir;
            this.Load();
        }

        IEnumerable<T> LoadCSV<T>(string fileName) where T: class, new() {
            string path = this.dir + "/" + fileName;
            var fileDesc = new CsvFileDescription {
                IgnoreUnknownColumns = true,
                // Needed to parse floats with decimal points, regardless of locale
                FileCultureName = "",
                MaximumNbrExceptions = 1,
            };
            var cc = new CsvContext();
            try {
                return cc.Read<T>(path, fileDesc).ToList();
            } catch(AggregatedException ae) {
                string msg;
                // Process all exceptions generated while processing the file
                List<Exception> innerExceptionsList =
                    (List<Exception>)ae.Data["InnerExceptionsList"];
                msg = "Error reading " + path + "\n" + String.Join("\n",
                        innerExceptionsList.Select(exc => exc.Message));
                throw new Invalid(msg);
            }
        }

        void Load() {
            this.Stops = this.LoadCSV<Stop>("stops.txt").ToDictionary(t => t.Id);
            this.Routes = this.LoadCSV<Route>("routes.txt").ToDictionary(t => t.Id);
            this.Services = this.LoadCSV<Service>("calendar.txt").ToDictionary(t => t.Id);
            this.Trips = this.LoadCSV<Trip>("trips.txt").ToDictionary(t => t.Id);
            var stopTimes = this.LoadCSV<StopTime>("stop_times.txt");

            
            // Resolve foreign keys into direct object references for convenience.
            foreach (var t in this.Trips.Values) {
                t.Route = this.Routes[t.RouteId];
                t.Service = this.Services[t.ServiceId];
            }
            foreach (var g in stopTimes.GroupBy(t => t.TripId)) {
                this.Trips[g.Key].StopTimes = g.OrderBy(t => t.Sequence).ToArray();
            }
        }
    }
}
