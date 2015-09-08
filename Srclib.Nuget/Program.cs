using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Srclib.Nuget
{
  public class Program
  {
    readonly IApplicationEnvironment _env;
    readonly IRuntimeEnvironment _runtimeEnv;
    readonly IAssemblyLoadContextAccessor _loadContextAccessor;

    public Program(IApplicationEnvironment env, IRuntimeEnvironment runtimeEnv, IAssemblyLoadContextAccessor loadContextAccessor)
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
      app.VersionOption("--version", () => _runtimeEnv.GetShortVersion(), () => _runtimeEnv.GetFullVersion());

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
