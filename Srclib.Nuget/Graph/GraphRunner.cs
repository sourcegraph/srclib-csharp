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
using System.Globalization;
using System.Xml.Linq;
using Srclib.Nuget.Documentation;

namespace Srclib.Nuget.Graph
{
  /// <summary>
  /// C# syntax walker that produces graph output.
  /// </summary>
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

    /// <summary>
    /// Add a definition to the output.
    /// Also adds a ref to the def, and
    /// potentially a doc.
    /// </summary>
    /// <param name="def">The def to add.</param>
    /// <param name="symbol">The symbol the def was created from.</param>
    void AddDef(Def def, Doc doc = null)
    {
      var r = Ref.AtDef(def);
      _output.Defs.Add(def);
      _output.Refs.Add(r);

      if (doc != null)
      {
        doc.UnitType = r.DefUnitType;
        doc.Unit = r.DefUnit;
        doc.Path = r.DefPath;
        _output.Docs.Add(doc);
      }
    }

    /// <summary>
    /// Scan a collection of resolved tokens and generate a ref for all 
    /// tokens that have a corresponding def 
    /// </summary>
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
        else
        {
          _defined.Add(definition);
          //at first encountering
          var def = Def.For(symbol: definition, type: "external", name: definition.Name).At(file, token.Span);
          //_output.Defs.Add(def);
          def.External = true;
          var reference = Ref.To(definition).At(file, token.Span);
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

      //If other languages are to be supported, this part needs to change.
      var roslynRef = (IRoslynMetadataReference)context.Export.MetadataReferences[0];
      var compilationRef = (CompilationReference)roslynRef.MetadataReference;
      var csCompilation = (CSharpCompilation)compilationRef.Compilation;
      context.Compilation = csCompilation;

      IEnumerable<LibraryDescription> deps = await DepresolveConsoleCommand.DepResolve(context.Project);


            HashSet<PortableExecutableReference> libs = new HashSet<PortableExecutableReference>();
            try
            {
                libs.Add(MetadataReference.CreateFromFile("/opt/DNX_BRANCH/runtimes/dnx-coreclr-linux-x64.1.0.0-rc1-update1/bin/mscorlib.dll"));
            }
            catch (Exception e)
            {
            }
            foreach (LibraryDescription ld in deps)
            {
                PortableExecutableReference r = null;
                try
                {
                    string path = ld.Path + "/" + DepresolveConsoleCommand.GetDll(ld.Path);
                    //Console.Error.WriteLine("trying to add reference to " + path);
                    r = MetadataReference.CreateFromFile(path);
                }
                catch (Exception e)
                {
                    //Console.Error.WriteLine("caught exception");
                    try
                    {
                        string name = ld.Identity.Name;
                        string path = ld.Path;
                        string cd = path.Substring(0, path.LastIndexOf('/'));
                        string newpath = cd + "/" + DepresolveConsoleCommand.GetDll(cd, name);
                        //Console.Error.WriteLine("trying to add reference to " + newpath);
                        r = MetadataReference.CreateFromFile(newpath);
                    }
                    catch (Exception ee)
                    {
                        //Console.Error.WriteLine("caught exception");
                    }
                }
                if (r != null)
                {
                    libs.Add(r);
                }
            }
            context.Compilation = context.Compilation.WithReferences(libs);


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

    /// <summary>
    /// Traverse AST node that represents class declaration
    /// </summary>
    /// <param name="node">AST node.</param>
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

          AddDef(def, DocProcessor.ForClass(symbol));
        }
      }

      base.VisitClassDeclaration(node);
    }

    /// <summary>
    /// Traverse AST node that represents interface declaration
    /// </summary>
    /// <param name="node">AST node.</param>
    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
      if (!node.Identifier.Span.IsEmpty)
      {
        var symbol = _sm.GetDeclaredSymbol(node);
        // Interfaces can be partial as well: this is a problem
        if (!_defined.Contains(symbol))
        {
          _defined.Add(symbol);

          var def = Def.For(symbol: symbol, type: "interface", name: symbol.Name)
            .At(_path, node.Identifier.Span);

          if (symbol.IsExported())
          {
            def.Exported = true;
          }

          AddDef(def, DocProcessor.ForClass(symbol));
        }
      }

      base.VisitInterfaceDeclaration(node);
    }

        /// <summary>
        /// Traverse AST node that represents struct declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (!node.Identifier.Span.IsEmpty)
            {
                var symbol = _sm.GetDeclaredSymbol(node);
                // Structs can also be partial
                if (!_defined.Contains(symbol))
                {
                    _defined.Add(symbol);

                    var def = Def.For(symbol: symbol, type: "struct", name: symbol.Name)
                      .At(_path, node.Identifier.Span);

                    if (symbol.IsExported())
                    {
                        def.Exported = true;
                    }

                    AddDef(def, DocProcessor.ForClass(symbol));
                }
            }
            base.VisitStructDeclaration(node);
        }


        /// <summary>
        /// Traverse AST node that represents enumeration declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            if (!node.Identifier.Span.IsEmpty)
            {
                var symbol = _sm.GetDeclaredSymbol(node);
                if (!_defined.Contains(symbol))
                {
                    _defined.Add(symbol);

                    var def = Def.For(symbol: symbol, type: "enum", name: symbol.Name)
                      .At(_path, node.Identifier.Span);

                    if (symbol.IsExported())
                    {
                        def.Exported = true;
                    }

                    AddDef(def, DocProcessor.ForClass(symbol));
                }
            }
            base.VisitEnumDeclaration(node);
        }

        /// <summary>
        /// Traverse AST node that represents enumeration constant declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            if (!node.Identifier.Span.IsEmpty)
            {
                var symbol = _sm.GetDeclaredSymbol(node);
                if (!_defined.Contains(symbol))
                {
                    _defined.Add(symbol);

                    var def = Def.For(symbol: symbol, type: "enum field", name: symbol.Name)
                      .At(_path, node.Identifier.Span);

                    def.Exported = true;
                    AddDef(def);
                }
            }
            base.VisitEnumMemberDeclaration(node);
        }


    /// <summary>
    /// Traverse AST node that represents method declaration
    /// </summary>
    /// <param name="node">AST node.</param>
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

        AddDef(def, DocProcessor.ForMethod(symbol));
      }

      base.VisitMethodDeclaration(node);
    }

    /// <summary>
    /// Traverse AST node that represents constructor declaration
    /// </summary>
    /// <param name="node">AST node.</param>
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

    /// <summary>
    /// Traverse AST node that represents property declaration
    /// </summary>
    /// <param name="node">AST node.</param>
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

    /// <summary>
    /// Traverse AST node that represents event declaration
    /// </summary>
    /// <param name="node">AST node.</param>
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

    /// <summary>
    /// Traverse AST node that represents method or constructor parameter
    /// </summary>
    /// <param name="node">AST node.</param>
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

    /// <summary>
    /// Traverse AST node that represents type parameter (in generic declarations)
    /// </summary>
    /// <param name="node">AST node.</param>
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

    /// <summary>
    /// Traverse AST node that represents field and variable declarations
    /// </summary>
    /// <param name="node">AST node.</param>
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

    /// <summary>
    /// If a token is resolved by parser, add a token to collection of resolved tokens
    /// </summary>
    /// <param name="token">token to check</param>
    public override void VisitToken(SyntaxToken token)
    {
      if (token.IsKind(SyntaxKind.IdentifierToken) || token.IsKind(SyntaxKind.IdentifierName))
      {
        var node = token.Parent;
        if (node == null)
        {
          goto skip;
        }

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
