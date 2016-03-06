using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;

namespace Srclib.Nuget
{
  class ScanConsoleCommand
  {
    public static void Register(CommandLineApplication cmdApp, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnvironment)
    {
      cmdApp.Command("scan", c => {
        c.Description = "Scan a directory tree and produce a JSON array of source units";

        c.HelpOption("-?|-h|--help");

        c.OnExecute(async () => {
          var dir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "."));

          var sourceUnits = new List<SourceUnit>();
          foreach(var proj in Scan(dir))
          {
            sourceUnits.Add(SourceUnit.FromProject(proj, dir));
          }

          Console.WriteLine(JsonConvert.SerializeObject(sourceUnits, Formatting.Indented));

          await DepresolveConsoleCommand.RunResolve(dir);

          return 0;
        });
      });
    }

    static IEnumerable<Project> Scan(string dir)
    {
      if (Project.HasProjectFile(dir))
      {
        Project proj;
        if (Project.TryGetProject(dir, out proj) && proj.CompilerServices == null)
          yield return proj;
        yield break;
      }

      foreach (var subdir in Directory.EnumerateDirectories(dir))
      {
        // Skip directories starting with .
        if (subdir.StartsWith("."))
          continue;

        foreach (var proj in Scan(subdir))
          yield return proj;
      }
    }
  }
}
