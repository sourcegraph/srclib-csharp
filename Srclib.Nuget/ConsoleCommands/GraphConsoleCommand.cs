using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;

namespace Srclib.Nuget
{
  class GraphConsoleCommand
  {
    public static void Register(CommandLineApplication cmdApp, IApplicationEnvironment appEnvironment)
    {
      cmdApp.Command("graph", c => {
        c.Description = "Resolve \"raw\" dependencies, such as the name and version of a dependency package, into a full specification of the dependency's target";

        c.HelpOption("-?|-h|--help");

        c.OnExecute(() => {
          var su = JsonConvert.DeserializeObject<SourceUnit>(Console.In.ReadToEnd());
          Console.WriteLine("{}");
          return 0;
        });
      });
    }
  }
}
