.PHONY: install dep

default: install

install:
	dnu restore

dep:
	dnu restore Srclib.Nuget/project.json
