using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Srclib.Nuget
{
  public class Program
  {
    readonly Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment _env;
    readonly Microsoft.Extensions.PlatformAbstractions.IRuntimeEnvironment _runtimeEnv;
    readonly Microsoft.Extensions.PlatformAbstractions.IAssemblyLoadContextAccessor _loadContextAccessor;

    public Program(Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment env, Microsoft.Extensions.PlatformAbstractions.IRuntimeEnvironment runtimeEnv, Microsoft.Extensions.PlatformAbstractions.IAssemblyLoadContextAccessor loadContextAccessor)
    {
      _env = env;
      _runtimeEnv = runtimeEnv;
      _loadContextAccessor = loadContextAccessor;
    }

    public int Main(string[] args)
    {
      var app = new CommandLineApplication(throwOnUnexpectedArg: true);
      app.Name = "srclib-csharp";
      app.FullName = "Scrlib C# toolchain";

      app.HelpOption("-?|-h|--help");

      // Show help information if no subcommand/option was specified
      app.OnExecute(() =>
      {
          app.ShowHelp();
          return 2;
      });

      ScanConsoleCommand.Register(app, _env);
      GraphConsoleCommand.Register(app, _env, _loadContextAccessor, _runtimeEnv);
      DepresolveConsoleCommand.Register(app, _env, _runtimeEnv);

      return app.Execute(args);
    }
  }
}
