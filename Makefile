
all: ccon.exe.so gtfs2graph.exe.so LINQtoCSV.dll.so

.PHONY: install-deps
install-deps:
	mkdir -p 3rd
	cd 3rd && nuget install -x ../packages.config
	#ln -f $$(ls -d docopt.net/lib/net[34]* | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/LINQtoCSV/lib/[Nn]et[34]* | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/YamlSerializer/lib/[Nn]et[34]* | tail -1)/*.dll .

gtfs2graph.exe: gtfs2graph.cs gtfs.cs
	mcs -debug+ -r:LINQtoCSV -r:YamlSerializer -O:all -out:$@ $^

ccon.exe: cli.cs
	mcs -r:YamlSerializer -O:all -out:$@ $^

%.so: %
	mono --debug --aot $<
