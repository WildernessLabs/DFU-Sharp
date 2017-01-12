all:
	dotnet restore
	(cd src/dfu-sharp && dotnet build)

test:
	(cd src/dfu-sharp.tests && dotnet build && dotnet test)
