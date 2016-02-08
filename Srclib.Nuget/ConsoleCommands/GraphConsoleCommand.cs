using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;
using Srclib.Nuget.Graph;

namespace Srclib.Nuget
{
  class GraphConsoleCommand
  {
    public static void Register(CommandLineApplication cmdApp, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnvironment, Microsoft.Extensions.PlatformAbstractions.IAssemblyLoadContextAccessor loadContextAccessor, Microsoft.Extensions.PlatformAbstractions.IRuntimeEnvironment runtimeEnvironment)
    {
      cmdApp.Command("graph", (Action<CommandLineApplication>)(c => {
        c.Description = "Perform parsing, static analysis, semantic analysis, and type inference";

        c.HelpOption("-?|-h|--help");

        c.OnExecute((Func<System.Threading.Tasks.Task<int>>)(async () => {
          var jsonIn = await Console.In.ReadToEndAsync();
          var sourceUnit = JsonConvert.DeserializeObject<SourceUnit>(jsonIn);

          var root = Directory.GetCurrentDirectory();
          var dir = Path.Combine(root, sourceUnit.Dir);
          var context = new GraphContext
          {
            RootPath = root,
            SourceUnit = sourceUnit,
            ProjectDirectory = dir,
            HostEnvironment = appEnvironment,
            LoadContextAccessor = loadContextAccessor,
            RuntimeEnvironment = runtimeEnvironment
          };

          var result = await GraphRunner.Graph(context);

          Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
          return 0;
        }));
      }));
    }
  }
}
