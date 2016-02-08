
## Requirements

srclib-csharp requires:

* DNX (.NET Execution Environment) aka ASP.NET 5
* .NET Core (coreclr)

Environmanet installation is described here: http://docs.asp.net/en/latest/getting-started/installing-on-linux.html

Briefly, you need to install dnvm

    curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh

DNX prerequisites, if necessary

    sudo apt-get install libunwind8 gettext libssl-dev libcurl4-openssl-dev zlib1g libicu-dev uuid-dev

and .NET Core

    dnvm upgrade -r coreclr

## Building

With DNX framework there is no explicit building, so just add a new toolchain to srclib by creating a symlink that points to srclib directory

    ln -s . $HOME/.srclib/sourcegraph.com/sourcegraph/srclib-csharp

You should also load project dependencies by running

    dnu restore Srclib.Nuget/project.json

or using

    make dep

which does exactly the same

## Testing

Run `git submodule update --init` the first time to fetch the submodule test
cases in `testdata/case`.

`srclib test` - run tests

`srclib test --gen` - regenerate new test data
