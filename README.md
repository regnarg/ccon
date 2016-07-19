ccon - Command-Line Public Transit Router
=========================================

About
-----

`ccon` is a program for finding connections in public transit networks.
It supports GTFS input and also comes with an experimental tool for
converting Czech train schedules in KANGO format to GTFS.

Written as a school project for the C# programming class at the [Faculty
of Mathematics and Physics][MFF], [Charles University][cuni], Prague

[MFF]: https://www.mff.cuni.cz/
[cuni]: https://www.cuni.cz/

Building
--------

`ccon` is written in C#. It was tested on Mono 4.4 on Linux but could in
principle work on other .NET platforms.

It depends on the following libraries:
  * [LINQtoCSV](http://www.codeproject.com/Articles/25133/LINQ-to-CSV-library)
  * [MsgPack-CLI](https://github.com/msgpack/msgpack-cli)
  * [CommandLineParser](https://github.com/gsscoder/commandline) 2.x prerelease
  * [Proj.NET](http://projnet.codeplex.com/)

You can automatically install them with the `make install-deps` command.

To build, just type `make`.

Installing
----------

There is no installation. Just drop `ccon.exe` and `ccon` somewhere in your
PATH.

Obtaining Timetables
--------------------

For the Czech Republic, the following timetables can be obtained:

  * The Prague Integrated Transit timetables published by DPP at

        ftp://jrdata:jrdata15@ftp.dpp.cz/

    The number in the password changes from time to time. Unfortunately,
    does not contain S trains.

  * The national train timetables from:

        ftp://ftp.cisjr.cz/draha/celostatni/

    They come in the KANGO format and can be converted into GTFS using
    the bundled kango2gtfs tool (incomplete).

For other cities/countries, try luck googling, website scrapping, nagging
your transit provider or requesting data with a Freedom of Information Act
request.

Preprocessing Timetables
------------------------

Before timetables can be used by `ccon`, they have to be preprocessed into
a binary format using the `ccon-build` tool.

By default, the built file is saved in `~/.cache/ccon.dat`. You can choose
a different path, but the you must specify a `--db` argument to `ccon` so
that it finds the data.

The recommended way of working with several timetable sets is to setup shell
aliases:

    alias pid='ccon --db=~/.cache/pid.dat'
    alias vlak='ccon --db=~/.cache/trains.dat'

The preprocessing step also outputs a `~/.cache/ccon.dat.comp` file with
a list of stop names suitable for shell completion. An example of integrating
the completion into the `fish` shell is included in the `ccon.fish` script.
A similar thing can be probably done for `bash`.

Searching Connections
---------------------

The basic usage of `ccon` is quite simple: `ccon FROM-STOP TO-STOP`.
The stops can be entered in a shortened way with rules similar to the Czech
on-line timetable portal IDOS. Each word that you enter must match a prefix
of the corresponding word in the stop name. All punctuation is treated as
equivalent to spaces (it is recommended to use dashes between words to
avoid quoting).

Example shortcuts:

    pelc    -> Pelc-Tyrolka
    malos   -> Malostranská
    malos-n -> Malostranské náměstí
    pra-sm  -> Praha-Smíchov
    p-s-n-k -> Praha-Smíchov Na Knížecí

There is a scoring mechanism: each exact word match (as opposed to a proper
prefix, e.g. "Pelc" in the example above) gives +1 point, extra words at the
end of stop name give -1 point. If more names match with the same highest
score, an error is raised.

You can also specify an arrival and/or departure time with `-a` and `-d`
options. The departure time specifies a lower limit, the arrival time
an upper limit. If both are specified, all connections satisfying the
constraints are shown. If only one bound is specified, by default only a few
connections are listed. Use `-A` to show all.

The routing algorithm currently has a hardcoded 2 minute minimum transfer
time.

Custom Aliases
--------------

You can specify custom stop aliases in the `~/.config/ccon.conf` file.
It is a simle text file with `alias = expansion format`. Example:

    ms = Malostranské náměstí
    karlov = Albertov/I.P.Pavlova
    kolej = pelc+3/kuchynka+5/trojska+10

The last two rows define so-called *virtual stops* that allow searching
connection to/from several nearby stops at once. The "+min" part is optional
and specifies the distance in minutes from the virtual point (e.g. your
home) to the given stop. The default to zero.

Known Issues
------------

  * Routing doesn't work for connections or vehicles that cross midnight.
  * The KANGO import currently does not support calendars, i.e., it doesn't
    discern between workdays, weekends and special days.
