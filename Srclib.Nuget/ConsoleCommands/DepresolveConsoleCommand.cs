using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Srclib.Nuget
{
  class DepresolveConsoleCommand
  {
    static Lazy<string> _dnuPath;

    public static void Register(CommandLineApplication cmdApp, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv, Microsoft.Extensions.PlatformAbstractions.IRuntimeEnvironment runtimeEnv)
    {
      if (runtimeEnv.OperatingSystem == "Windows")
      {
        _dnuPath = new Lazy<string>(FindDnuWindows);
      }
      else
      {
        _dnuPath = new Lazy<string>(FindDnuNix);
      }

      cmdApp.Command("depresolve", c => {
        c.Description = "Perform a combination of parsing, static analysis, semantic analysis, and type inference";

        c.HelpOption("-?|-h|--help");

        c.OnExecute(async () => {
          //System.Diagnostics.Debugger.Launch();
          var jsonIn = await Console.In.ReadToEndAsync();
          var sourceUnit = JsonConvert.DeserializeObject<SourceUnit>(jsonIn);

          var dir = Path.Combine(Directory.GetCurrentDirectory(), sourceUnit.Dir);
          var deps = await DepResolve(dir);

          var result = new List<Resolution>();
          foreach(var dep in deps)
          {
            result.Add(Resolution.FromLibrary(dep));
          }

          Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
          return 0;
        });
      });
    }

    static async Task<IEnumerable<LibraryDescription>> DepResolve(string dir)
    {
      Project proj;
      if(!Project.TryGetProject(dir, out proj))
      {
        throw new Exception("Error reading project.json");
      }

      var allDeps = GetAllDeps(proj);
      if (allDeps.Any(dep => !dep.Resolved))
      {
        await RunResolve(dir);
        allDeps = GetAllDeps(proj);
      }

      return allDeps;
    }

    static IEnumerable<LibraryDescription> GetAllDeps(Project proj) =>
      proj.GetTargetFrameworks().Select(f => f.FrameworkName)
        .SelectMany(f =>
        {
          var context = new ApplicationHostContext
          {
            Project = proj,
            TargetFramework = f
          };

          return ApplicationHostContext.GetRuntimeLibraries(context).Skip(1); // the first one is the self-reference
        })
        .Distinct(LibraryUtils.Comparer)
        .OrderBy(l => l.Identity?.Name);

    static async Task RunResolve(string dir)
    {
      var p = new Process();
      p.StartInfo.WorkingDirectory = dir;
      p.StartInfo.FileName = _dnuPath.Value;
      p.StartInfo.Arguments = "restore";
      p.StartInfo.UseShellExecute = false;
      p.StartInfo.CreateNoWindow = true;
      p.StartInfo.RedirectStandardInput = true;
      p.StartInfo.RedirectStandardOutput = true;
      p.StartInfo.RedirectStandardError = true;

      p.Start();
      // it's important to read stdout and stderr, else it might deadlock
      var outs = await Task.WhenAll(p.StandardOutput.ReadToEndAsync(), p.StandardError.ReadToEndAsync());
      p.WaitForExit();

      // in the future, actually parse output or something
      //Console.WriteLine(outs[0]);
    }

    static string FindDnuWindows()
    {
      return RunForResult(@"C:\Windows\System32\cmd.exe", "/c \"where dnu\"");
    }

    static string FindDnuNix()
    {
      return RunForResult("/bin/bash", "-c \"which dnu\"");
    }

    static string RunForResult(string shell, string command)
    {
      var p = new Process();
      p.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
      p.StartInfo.FileName = shell;
      p.StartInfo.Arguments = command;
      p.StartInfo.UseShellExecute = false;
      p.StartInfo.CreateNoWindow = true;
      p.StartInfo.RedirectStandardInput = true;
      p.StartInfo.RedirectStandardOutput = true;
      p.StartInfo.RedirectStandardError = false;

      p.Start();
      var result = p.StandardOutput.ReadToEnd().Trim();
      p.WaitForExit();
      return result;
    }
  }
}
