.PHONY: build test

build:
	mcs main.cs VJP.cs option/*cs -langversion:ISO-2 -out:vjp.exe

test:
	./test.sh
