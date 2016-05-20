.PHONY: install dep

default: install

install:
	cd Srclib.Nuget && dnu restore

dep:
	dnu restore Srclib.Nuget/project.json
