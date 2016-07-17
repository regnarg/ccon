
#all: ccon.exe.so gtfs2graph.exe.so LINQtoCSV.dll.so YamlSerializer.dll.so
all: ccon.exe ccon-build.exe model_console.exe

PY_REFS=-lib:/usr/lib/ipy -r:Microsoft.Scripting -r:IronPython
CSFLAGS_DBG=-debug+ -d:DEBUG
CSFLAGS_COMMON=-O:all,-shared
CSFLAGS=$(CSFLAGS_COMMON) $(CSFLAGS_DBG)

.PHONY: install-deps
install-deps:
	mkdir -p 3rd
	cd 3rd && nuget install -x ../packages.config
	#ln -f $$(ls -d docopt.net/lib/net[34]* | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/LINQtoCSV/lib/[Nn]et[34]* | tail -1)/*.dll .
	#ln -f $$(ls -d 3rd/YamlSerializer/lib/[Nn]et[34]* | tail -1)/*.dll .
	#ln -f $$(ls -d 3rd/QuickGraph/lib/[Nn]et[34]* | tail -1)/*.dll .
	#ln -f 3rd/TreeLibInterface/lib/*.dll .
	#ln -f 3rd/TreeLib/lib/*.dll .

# -r:TreeLibInterface -r:TreeLib 
ccon-build.exe: builder.cs gtfs.cs model.cs utils.cs
	mcs $(CSFLAGS) -r:LINQtoCSV -r:MsgPack $(PY_REFS) -out:$@ $^

ccon.exe: cli.cs model.cs utils.cs
	mcs $(CSFLAGS) -r:MsgPack $(PY_REFS) -out:$@ $^

model_console.exe: model_console.cs model.cs utils.cs
	mcs $(CSFLAGS) -r:MsgPack $(PY_REFS) -out:$@ $^

%.so: %
	mono --debug --aot -O=all,-shared $<
