.PHONY: build test

build:
	mcs main.cs vjp/*cs -langversion:ISO-2 -out:vjp.exe

test:
	./test.sh
