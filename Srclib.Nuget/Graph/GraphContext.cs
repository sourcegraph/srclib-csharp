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
    public IApplicationEnvironment HostEnvironment { get; set; }
    public IApplicationEnvironment ApplicationEnvironment { get; set; }
    public IFileWatcher FileWatcher { get; set; }
    public IAssemblyLoadContextAccessor LoadContextAccessor { get; set; }
    public CompilationEngineContext CompilationContext { get; set; }
    public CompilationEngine CompilationEngine { get; set; }
    public ILibraryExporter LibraryExporter { get; set; }
    public LibraryExport Export { get; set; }
    public CSharpCompilation Compilation { get; set; }
    public string RootPath { get; set; }
    public IRuntimeEnvironment RuntimeEnvironment { get; set; }
  }
}
