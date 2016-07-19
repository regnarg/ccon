`ccon` design
=============

For details about what `ccon` is and what it does, see `README.md`.

*Note: Portions of this text, ideas and pictures are taken from the official
solution of KSP task [28-2-6][0], written a few months ago by me.*

[0]: https://ksp.mff.cuni.cz/viz/28-2-6/reseni

GTFS
----

`ccon` consumes transit timetables in the [GTFS][1] interchange format as
input. A GTFS feed describes the following kinds of entities:

  * **Trips** are the most basic unit of GTFS. A trip describes one journey of
    a vehicle between terminal stations. A trip is described by a set of *stop
    times*, i.e., triplets (stop, arrival time, departure time).
  * **Stops.** Each stop has an identifier, name and optionally geographic
    location. Stops might be rather fine-grained. For example, in the Prague
    GTFS, each stop sign corresponds to an individual stop object:
    opposite-direction stops count as different, stops at perpendicular arms of
    a crossroads are considered as different.
  * **Routes** describe regular paths taken by vehicles (what is usually called
    a "line", e.g. 12 or C in Prague). Routes are not used for routing as the
    complete information about the path taken by a vehicle is stored in the
    trip. A route is merely a logical grouping of related trips with a name.
  * **Calendars** (also called *services*) describe on which days vehicles
    operate and on which they don't. Each trip has exactly one calendar
    associated, multiple vehicles can use one calendar. Examples of calendars
    might be "every workday" or "friday to sunday in the summer season."

[1]: https://developers.google.com/transit/gtfs/reference/

Network Representation
----------------------

The most straigtforward timetable routing scheme is based on converting the
timetable into a state space, i.e. a graph whose vertices are (stop, time)
pairs. Edges between those vertices represent possible actions (taking a ride
to another stop, or waiting for a later time). For example, imagine a network
with five stops (A-E) and two lines: solid (runs every 20min) and dashed (every
30m) like this:

![example network](net.pdf)

If this was a snippet of the stop's timetable (numbers are times in minutes since
midnight):

    A 300 B 305 C 310
    A 320 B 325 C 330
    A 340 B 345 C 350
    C 300 B 305 A 310
    C 320 B 325 A 330
    C 340 B 345 A 350
    A 300 D 307 B 315 E 320
    A 330 D 337 B 345 E 350
    E 300 B 305 D 313 A 320
    E 330 B 335 D 343 A 350

Each line describes one trip. For simplicity, we consider the departure and arrival
times the same in this example. The timetable above would be converted into the
following state graph:

![state graph](state.pdf)

The full edges are "ride" edges, the vertical dotted edges represent
waiting on a stop and connect all the (stop, time, -) vertices for
a given stop in a time-sorted linked list.

Oriented paths in this graph describe exactly all the possible travels
thru the network. And it is a DAG, so we can compute almost anything on
it in linear time.

Because we want to ensure a minimum transfer time, our possible actions
depend not only on which stop we are and when but also how we arrived
there (we can continue in the same vehicle but not in another vehicle
that departs at the same time).

Therefore, we will use (stop, time, trip) tuples as our states, representing
the fact that we are currently standing in the vehicle described by `trip`
at stop `stop` on time `time`. Trip can be "-" (null), representing the fact
that we are standing outside at a stop.

Now there are several kinds of edges:

  * Waiting edges: `(stop, times[i], -) -> (stop, times[i+1], -)
  * Ride edges: go to the next stop of the same trip:
      - (trip.path[i].stop, trip.path[i].arrtime, trip) -> (trip.path[i].stop, trip.path[i].deptime, trip)
      - (trip.path[i].stop, trip.path[i].deptime, trip) -> (trip.path[i+1].stop, trip.path[i+1].arrtime, trip)
  * Getting-on edges:
      - (stop, deptime, -) -> (stop, deptime, trip) for each (stop, deptime) of a trip
  * Getting-off edges:
      - (stop, arrtime, trip) -> (stop, arrtime+DELTA, trip), where DELTA
        is the minimum transfer time. This guarantees that if we get on another
        vehicle, it will be at least DELTA minutes after getting off the previous
        one.

A smallish portion of such state graph can be seen on the following picture:

![state graph with transfer times](xfer.pdf)

Again, full edges are rides, dotted waits and dashed represent getting on/off
the vehicle. Oriented path in this graph represent all the possible travels that
respect the set minimum transfer time.

Overall Architecture
--------------------

`ccon` comprises of several independent programs:

  * **`ccon-build.exe`** (`builder.cs`) takes GTFS input, builds the state graph
    and saves a compact binary representation thereof.
  * **`ccon.exe`** (`cli.cs`, `routing.cs`) uses the pregenerated graph to look up
    connections.
  * **`kango2gtfs.exe`** (`kango2gtfs.cs`) converts KANGO Czech train timetables
    into GTFS

There are also a few common modules:

  * **`model.cs`** defines types for holding a representation of the network graph
    and methods to loading/saving the from/to disk (used by `ccon-build` and
    `ccon`)
  * **`gtfs.cs`** defines classes representing GTFS entities that can be used
    for loading (in `builder.cs`) and saving (in `kango2gtfs.cs`) them
  * **`utils.cs`** contains small helper functions and a few common constants.
    It is used by all the programs.

Graph Storage
-------------

The state graph tends to get rather big. For the Prage Integrated Transport
dataset it has about 3 million vertices and 10 million edges (counting also
pedestrian transfers between close stops).

Storing such a large graph is problematic both in RAM and on disk. If we
represent it with objects and references, it takes about 1.5GB of memory and it
takes about 10 seconds just to allocate all the objects.

An array-of-structures representation is much more compact and faster to
construct. For easier manipulation, `ccon-builder` first constructs the graph
as objects and then converts it to a bunch of structure arrays referencing each
other by indexes. This process is orchestrated by the CompactTableBuilder
helper class. The resulting graph takes ~150 MB of memory.

Saving these structure arrays to disk also proved challenging. These are the
methods we tried, along with the results:

Method                           Time to load      Size on disk
-------------------------------  ----------------  --------------
Serialization (BinaryFormatter)  >30 s, fails[^1]  > 500 M, fails
MsgPack                          9.8 s             50 M
BinaryWriter                     0.5 s             60 M
MemoryMappedFile (WriteArray)    0.01-0.1 s        60 M

[^1]: The BinaryFormatter always crashes when serializing an array
      with more than ~6M items:
      http://www.thelowlyprogrammer.com/2010/02/straining-limits-of-c.html

So we went with the MemoryMappedFile in the end. The code got a lot more
complex since then so those number are only for relative comparison.
Small metadata (e.g. list of stops) are still saved using MsgPack because
it is easier to work with. But the graph itself (vertices and edges)
is saved using MemoryMappedFiles.

We use the common trick of saving the successors of all the vertices in one big
array and for each vertex remember where it successors start in that array (the
end is implicit by the next vertex's start). We also use a few "compressing"
trick, e.g. save times with 5s granularity to fit it into an `ushort` or save
both the calendar and route reference into one `ushort`.

The Search Algorithm
--------------------

## The Goal

We want to find all the fastest connections between a pair of stops. This
could be defined by a hypothetical construction: Take all possible connection
between the pair of stops, each assign a time interval $T_i=(d_i,a_i)$, where
$d_i$ and $a_i$ are original departure and final arrival times of the connection.
Then whenever one connection's interval wholly contains another ($T_i \subset T_j$),
drop the outer connection ($T_j$). The remaining connections may overlap but there
is a well-defined linear order on them, because $d_i \le d_j \Leftrightarrow a_i \le a_j$.
Additionaly, for each possible $(d, a)$ pair we keep only one connection as
there could be an exponential number of them, more or less one as good as another.

This gives a list of all the time-unique shortest connections between the stops
during the whole day. The list can be then simply filtered based on requested
departure/arrival times.

This is what the algorithm should find but of course not *how* it should find it.

## The Method

The algorithm is rather simple: for each vertex of the starting stop, in reverse
time order, traverse the part of the graph reachable from it and mark it with
the start time (unless already marked). In each iteration, we mark all vertices
that can be travelled to if we depart at the given time but not if we depart
later. Therefore, for each vertex, we have computed the latest possible moment
that we can depart from the starting stop and still get to that vertex.

Now it suffices to traverse the vertices of the destination stop. For each of
them, we know the latest possible time departure that allows us to arrive to
the destination at the given moment. That corresponds to a shortest travel
from start to destination. And if we record predecesors (which vertex we came
from) for each vertex, we can reconstruct the path by simply walking backward
the predecesor chain.

All this in linear time, as each vertex is visited at most one and its neighbours
explored at most once.

Virtual stops make this a little harder but essentially similar. If the start
is a virtual stop, we take vertices from all the variants and sort them by the
shifted time (i.e., the time that you would have to leave your house / the
virtual place). This gives us, again, for each vertex, the lastest possible time
we can leave the virtual place and still get there. Virtual destinations are
rather similar.
