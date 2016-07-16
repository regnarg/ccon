
#all: ccon.exe.so gtfs2graph.exe.so LINQtoCSV.dll.so YamlSerializer.dll.so
all: ccon.exe ccon-build.exe

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

# -r:TreeLibInterface -r:TreeLib 
ccon-build.exe: builder.cs gtfs.cs model.cs utils.cs
	mcs -debug+ -r:LINQtoCSV -r:MsgPack $(PY_REFS) -O:all -out:$@ $^

ccon.exe: cli.cs
	mcs -O:all,-shared $(PY_REFS) -out:$@ $^

%.so: %
	mono --debug --aot -O=all,-shared $<
