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

        var repoName = c.Option ("--repo <REPOSITORY_URL>",   "The URI of the repository that contains the directory tree being scanned", CommandOptionType.SingleValue);
        var subdir   = c.Option ("--subdir <SUBDIR_PATH>", "The path of the current directory (in which the scanner is run), relative to the root directory of the repository being scanned", CommandOptionType.SingleValue);

        c.HelpOption("-?|-h|--help");

        c.OnExecute(() => {
          var repository = repoName.Value();
          var dir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), subdir.Value()));

          var sourceUnits = new List<SourceUnit>();
          foreach(var proj in Scan(dir))
          {
            sourceUnits.Add(SourceUnit.FromProject(proj, dir));
          }

          Console.WriteLine(JsonConvert.SerializeObject(sourceUnits, Formatting.Indented));

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
