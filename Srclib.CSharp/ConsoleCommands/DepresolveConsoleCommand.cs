using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;

namespace Srclib.CSharp
{
  class DepresolveConsoleCommand
  {
    public static void Register(CommandLineApplication cmdApp, IApplicationEnvironment appEnvironment)
    {
      cmdApp.Command("depresolve", c => {
        c.Description = "Perform a combination of parsing, static analysis, semantic analysis, and type inference";
        
        c.HelpOption("-?|-h|--help");
        
        c.OnExecute(() => {
          Console.WriteLine("{}");
          return 0;
        });
      });
    }
  }
}