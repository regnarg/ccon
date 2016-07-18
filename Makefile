
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
	ln -f $$(ls -d 3rd/LinqToCsv/lib/[Nn]et[234]* | sort -f | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/ProjNet/lib/[Nn]et[234]* | sort -f | tail -1)/*.dll .
	ln -f $$(ls -d 3rd/CommandLineParser/lib/[Nn]et[234]* | sort -f | tail -1)/*.dll .
	mkdir -p 3rd/msgpack-cli && cd 3rd/msgpack-cli && wget https://github.com/msgpack/msgpack-cli/releases/download/0.7.1/MsgPack.Cli.0.7.1.zip && unzip MsgPack.Cli.0.7.1.zip || true
	ln -f $$(ls -d 3rd/msgpack-cli/bin/[Nn]et[234]* | sort -f | tail -1)/*.dll .

ccon-build.exe: builder.cs gtfs.cs model.cs utils.cs
	mcs $(CSFLAGS) -r:LINQtoCSV -r:MsgPack -r:ProjNet $(PY_REFS) -out:$@ $^

ccon.exe: cli.cs model.cs utils.cs routing.cs
	mcs $(CSFLAGS) -r:MsgPack -r:CommandLine $(PY_REFS) -out:$@ $^

model_console.exe: model_console.cs model.cs utils.cs
	mcs $(CSFLAGS) -r:MsgPack $(PY_REFS) -out:$@ $^

%.so: %
	mono --debug --aot -O=all,-shared $<
