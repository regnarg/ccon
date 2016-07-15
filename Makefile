
#all: ccon.exe.so gtfs2graph.exe.so LINQtoCSV.dll.so YamlSerializer.dll.so
all: ccon.exe gtfs2graph.exe

PY_REFS=-lib:/usr/lib/ipy -r:Microsoft.Scripting -r:IronPython

.PHONY: install-deps
install-deps:
	mkdir -p 3rd
	cd 3rd && nuget install -x ../packages.config
	#ln -f $$(ls -d docopt.net/lib/net[34]* | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/LINQtoCSV/lib/[Nn]et[34]* | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/YamlSerializer/lib/[Nn]et[34]* | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/QuickGraph/lib/[Nn]et[34]* | tail -1)/*.dll .
	ln -f 3rd/TreeLibInterface/lib/*.dll .
	ln -f 3rd/TreeLib/lib/*.dll .

gtfs2graph.exe: gtfs2graph.cs gtfs.cs graph.cs utils.cs
	mcs -debug+ -r:LINQtoCSV -r:TreeLibInterface -r:TreeLib -r:QuickGraph -r:QuickGraph.Serialization $(PY_REFS) -O:all -out:$@ $^

ccon.exe: cli.cs
	mcs -O:all,-shared $(PY_REFS) -out:$@ $^

%.so: %
	mono --debug --aot -O=all,-shared $<
