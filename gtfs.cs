using System;
using LINQtoCSV;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static CCon.Utils;

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
        public class Calendar {
            [CsvColumn(Name="service_id")]
            public string Id;
            [CsvColumn(Name="route_short_name")]
            public string ShortName;
        }
        public class StopTime {
            [CsvColumn(Name="trip_id")]
            public string TripId;

            [CsvColumn(Name="arrival_time")]
            public string ArrTimeStr {
                set { ArrTime = ParseTime(value); }
                get { return FormatTime(ArrTime); }
            }
            public ushort ArrTime;

            [CsvColumn(Name="departure_time")]
            public string DepTimeStr {
                set { DepTime = ParseTime(value); }
                get { return FormatTime(DepTime); }
            }
            public ushort DepTime;

            [CsvColumn(Name="stop_id")]
            public string StopId;
            public Stop Stop;
            [CsvColumn(Name="sequence")]
            public int Sequence;
        }
        public class Trip {
            [CsvColumn(Name="trip_id")]
            public string Id;
            [CsvColumn(Name="route_id")]
            public string RouteId;
            public Route Route;
            [CsvColumn(Name="service_id")]
            public string CalendarId;
            public Calendar Calendar;

            public StopTime[] StopTimes;
        }

        public Dictionary<string, Stop> Stops;
        public Dictionary<string, Route> Routes;
        public Dictionary<string, Calendar> Calendars; 
        public Dictionary<string, Trip> Trips; 

        IEnumerable<T> LoadCSV<T>(string path) where T: class, new() {
            using (new Profiler("Loading " + path)) {
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
        }

        public void Load(string dir) {
            string b = dir + "/";
            IEnumerable<StopTime> stopTimes;
            using (new Profiler("Load all GTFS tables")) {
                this.Stops = this.LoadCSV<Stop>(b+"stops.txt").ToDictionary(t => t.Id);
                this.Routes = this.LoadCSV<Route>(b+"routes.txt").ToDictionary(t => t.Id);
                this.Calendars = this.LoadCSV<Calendar>(b+"calendar.txt").ToDictionary(t => t.Id);
                this.Trips = this.LoadCSV<Trip>(b+"trips.txt").ToDictionary(t => t.Id);
                stopTimes = this.LoadCSV<StopTime>(b+"stop_times.txt");
            }
            
            // Resolve foreign keys into direct object references for convenience.
            foreach (var t in this.Trips.Values) {
                t.Route = this.Routes[t.RouteId];
                t.Calendar = this.Calendars[t.CalendarId];
            }
            foreach (var t in stopTimes) {
                t.Stop = this.Stops[t.StopId];
            }
            foreach (var g in stopTimes.GroupBy(t => t.TripId)) {
                this.Trips[g.Key].StopTimes = g.OrderBy(t => t.Sequence).ToArray();
            }
        }
    }
}
