using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Dnx.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Srclib.Nuget.Graph
{
  public class GraphRunner : CSharpSyntaxWalker
  {
    readonly Output _output = new Output();
    readonly List<Tuple<SyntaxToken, ISymbol, string>> _refs = new List<Tuple<SyntaxToken, ISymbol, string>>();
    readonly HashSet<ISymbol> _defined = new HashSet<ISymbol>();
    SemanticModel _sm;
    string _path;

    private GraphRunner()
      : base(SyntaxWalkerDepth.Token)
    {
    }

    void AddDef(Def def)
    {
      var r = Ref.AtDef(def);
      _output.Defs.Add(def);
      _output.Refs.Add(r);
    }

    void RunTokens()
    {
      foreach(var r in _refs)
      {
        var token = r.Item1;
        var definition = r.Item2;
        var file = r.Item3;

        if (_defined.Contains(definition))
        {
          var reference = Ref.To(definition)
            .At(file, token.Span);

          _output.Refs.Add(reference);
        }
      }
    }

    internal static async Task<Output> Graph(GraphContext context)
    {
      if (context.Project == null)
      {
        Project project;
        if (!Project.TryGetProject(context.ProjectDirectory, out project))
          throw new InvalidOperationException("Can't find project");

        context.Project = project;
      }

      context.ApplicationHostContext = new ApplicationHostContext
      {
        ProjectDirectory = context.ProjectDirectory,
        Project = context.Project,
        TargetFramework = context.Project.GetTargetFrameworks().First().FrameworkName
      };

      context.ApplicationEnvironment = new ApplicationEnvironment(
        context.Project,
        context.ApplicationHostContext.TargetFramework,
        "Debug",
        context.HostEnvironment);

      context.CompilationContext = new CompilationEngineContext(context.ApplicationEnvironment, context.RuntimeEnvironment, context.LoadContextAccessor.Default, new CompilationCache());

      context.CompilationEngine = new CompilationEngine(context.CompilationContext);
      context.LibraryExporter = context.CompilationEngine.CreateProjectExporter(context.Project, context.ApplicationHostContext.TargetFramework, "Debug");
      context.Export = context.LibraryExporter.GetExport(context.Project.Name);

      // TODO: If other languages are to be supported, this part needs to change.
      var roslynRef = (IRoslynMetadataReference)context.Export.MetadataReferences[0];
      var compilationRef = (CompilationReference)roslynRef.MetadataReference;
      var csCompilation = (CSharpCompilation)compilationRef.Compilation;
      context.Compilation = csCompilation;

      var runner = new GraphRunner();
      foreach (var st in context.Compilation.SyntaxTrees)
      {
        var path = st.FilePath;
        if (!string.IsNullOrWhiteSpace(path))
        {
          path = Utils.GetRelativePath(path, context.RootPath);
          if (!string.IsNullOrWhiteSpace(path) && path.Substring(0, 3) != ".." + Path.DirectorySeparatorChar)
          {
            // this is a source code file we want to grap
            runner._sm = context.Compilation.GetSemanticModel(st, false);
            runner._path = path;
            var root = await st.GetRootAsync();
            runner.Visit(root);
          }
        }
      }

      runner._sm = null;
      runner._path = null;
      runner.RunTokens();
      return runner._output;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);
        // Classes can be partial, however, currently, srclib only support one definition per symbol
        if (!_defined.Contains(symbol))
        {
          _defined.Add(symbol);

          var def = Def.For(symbol: symbol, type: "class", name: symbol.Name)
            .At(_path, node.Identifier.Span);

          if (symbol.IsExported())
          {
            def.Exported = true;
          }

          AddDef(def);
        }
      }

      base.VisitClassDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        var def = Def.For(symbol: symbol, type: "method", name: symbol.Name)
          .At(_path, node.Identifier.Span);

        if (symbol.IsExported())
        {
          def.Exported = true;
        }

        AddDef(def);
      }

      base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        var def = Def.For(symbol: symbol, type: "ctor", name: symbol.Name)
          .At(_path, node.Identifier.Span);

        if (symbol.IsExported())
        {
          def.Exported = true;
        }

        AddDef(def);
      }

      base.VisitConstructorDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        var def = Def.For(symbol: symbol, type: "property", name: symbol.Name)
          .At(_path, node.Identifier.Span);

        if (symbol.IsExported())
        {
          def.Exported = true;
        }

        AddDef(def);
      }

      base.VisitPropertyDeclaration(node);
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        var def = Def.For(symbol: symbol, type: "event", name: symbol.Name)
          .At(_path, node.Identifier.Span);

        if (symbol.IsExported())
        {
          def.Exported = true;
        }

        AddDef(def);
      }

      base.VisitEventDeclaration(node);
    }

    public override void VisitParameter(ParameterSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        var def = Def.For(symbol: symbol, type: "param", name: symbol.Name)
          .At(_path, node.Identifier.Span);

        def.Local = true;

        AddDef(def);
      }

      base.VisitParameter(node);
    }

    public override void VisitTypeParameter(TypeParameterSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        var def = Def.For(symbol: symbol, type: "typeparam", name: symbol.Name)
          .At(_path, node.Identifier.Span);

        def.Local = true;

        AddDef(def);
      }

      base.VisitTypeParameter(node);
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);

        _defined.Add(symbol);

        string type;
        bool local = false;
        bool exported = false;
        if (symbol is ILocalSymbol)
        {
          type = "local";
          local = true;
        }
        else if (symbol is IFieldSymbol)
        {
          type = "field";
          exported = symbol.IsExported();

          if(((IFieldSymbol)symbol).IsConst)
          {
            type = "const";
          }
        }
        else
        {
          goto skip;
        }

        var def = Def.For(symbol: symbol, type: type, name: symbol.Name)
          .At(_path, node.Identifier.Span);

        def.Local = local;
        def.Exported = exported;

        AddDef(def);
      }

      skip:
      base.VisitVariableDeclarator(node);
    }

    public override void VisitToken(SyntaxToken token)
    {
      if (token.IsKind(SyntaxKind.IdentifierToken) || token.IsKind(SyntaxKind.IdentifierName))
      {
        var node = token.Parent;
        if (node == null)
          goto skip;

        var symbol = _sm.GetSymbolInfo(node);
        if (symbol.Symbol == null)
        {
          goto skip;
        }

        var definition = symbol.Symbol.OriginalDefinition;
        _refs.Add(Tuple.Create(token, definition, _path));
      }

      skip:
      base.VisitToken(token);
    }
  }
}
