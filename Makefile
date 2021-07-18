.PHONY: build test

build:
	mcs main.cs Runtime/Jonson.cs option/*cs -langversion:ISO-2 -out:jonson.exe

test:
	./test.sh
