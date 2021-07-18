.PHONY: build test

build:
	mcs Tests/Runtime/main.cs Runtime/Jonson.cs option/*cs -langversion:ISO-2 -out:jonson.exe

test:
	./Tests/Runtime/test.sh
