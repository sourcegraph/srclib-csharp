using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;


namespace Srclib.Nuget.Graph
{
  public class GraphContext
  {
    public SourceUnit SourceUnit { get; set; }
    public string ProjectDirectory { get; set; }
    public Project Project { get; set; }
    public ApplicationHostContext ApplicationHostContext { get; set; }
    public Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment HostEnvironment { get; set; }
    public Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment ApplicationEnvironment { get; set; }
    public Microsoft.Extensions.PlatformAbstractions.IAssemblyLoadContextAccessor LoadContextAccessor { get; set; }
    public CompilationEngineContext CompilationContext { get; set; }
    public CompilationEngine CompilationEngine { get; set; }
    public ILibraryExporter LibraryExporter { get; set; }
    public LibraryExport Export { get; set; }
    public CSharpCompilation Compilation { get; set; }
    public string RootPath { get; set; }
    public Microsoft.Extensions.PlatformAbstractions.IRuntimeEnvironment RuntimeEnvironment { get; set; }
  }
}
