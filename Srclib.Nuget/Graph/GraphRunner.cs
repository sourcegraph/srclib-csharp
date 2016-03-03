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

using Newtonsoft.Json;

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
        readonly HashSet<string> keys = new HashSet<string>();
        readonly static Dictionary<string, string> dllToProjectUrl = new Dictionary<string, string>();
        readonly static Dictionary<string, string> projectUrlToRepo = new Dictionary<string, string>();

        SemanticModel _sm;
        string _path;

        private GraphRunner() : base(SyntaxWalkerDepth.Token)
        {
            projectUrlToRepo["http://www.newtonsoft.com/json"] = "github.com/JamesNK/Newtonsoft.Json";
            projectUrlToRepo["http://autofac.org/"] = "github.com/autofac/Autofac";
            projectUrlToRepo["http://msdn.com/roslyn"] = "github.com/dotnet/roslyn";


            // multiple dlls from different repositories have www.asp.net as projectUrl
            // so we probably need a multimap here
            // TODO($ildarisaev): implement a correct mapping for all dlls
            projectUrlToRepo["http://www.asp.net/"] = "github.com/aspnet/dnx";
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
            string key = def.DefKey;
            if (!keys.Contains(key))
            {
                keys.Add(key);
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
        }

        /// <summary>
        /// Scan a collection of resolved tokens and generate a ref for all 
        /// tokens that have a corresponding def or set up a link to external repo
        /// </summary>
        void RunTokens()
        {
            foreach(var r in _refs)
            {
                try
                {
                    var token = r.Item1;
                    var definition = r.Item2;
                    var file = r.Item3;
                    if (_defined.Contains(definition))
                    {
                        var reference = Ref.To(definition).At(file, token.Span);
                        _output.Refs.Add(reference);
                    }
                    else
                    {
                        _defined.Add(definition);
                        var reference = Ref.To(definition).At(file, token.Span);
                        if ((definition.ContainingAssembly != null) && (definition.ContainingAssembly.Identity != null) && (definition.ContainingAssembly.Identity.Name != null))
                        {
                            if (dllToProjectUrl.ContainsKey(definition.ContainingAssembly.Identity.Name + ".dll"))
                            {
                                string url = dllToProjectUrl[definition.ContainingAssembly.Identity.Name + ".dll"];
                                if (projectUrlToRepo.ContainsKey(url))
                                {
                                    url = projectUrlToRepo[url];
                                }
                                // Sourcegraph needs to clone a repo, so filter out non cloneable urls
                                // Currently consider only github urls as cloneable
                                // TODO($ildarisaev): think of a more sophisticated check
                                if (url.IndexOf("github.com") != -1)
                                {
                                    reference.DefRepo = url;
                                    reference.DefUnit = definition.ContainingAssembly.Identity.Name;
                                    reference.DefUnitType = "NugetPackage";
                                }
                            }
                        }
                        _output.Refs.Add(reference);
                    }
                }
                catch (Exception e)
                {

                }
            }
        }

        internal static void HandleNuspec(string path, string dll)
        {
            string nuspec = DepresolveConsoleCommand.RunForResult("/bin/bash", "-c \"cd " + path + " && find -name '*.nuspec' | head -1\"");
            var content = File.ReadAllText(path + "/" + nuspec);
            int i = content.IndexOf("<projectUrl>");
            if (i != -1)
            {
                int j = content.IndexOf("</projectUrl>");
                string projectUrl = content.Substring(i + 12, j - i - 12);
                dllToProjectUrl[dll + ".dll"] = projectUrl;
            }
        }

        internal static async Task<Output> Graph(GraphContext context)
        {
            if (context.Project == null)
            {
                Project project;
                if (!Project.TryGetProject(context.ProjectDirectory, out project))
                {
                    throw new InvalidOperationException("Can't find project");
                }

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

            if (!(context.Export.MetadataReferences[0] is IRoslynMetadataReference))
            {
                return new Output();
            }
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
                    string path = "";
                    if (ld.Path.EndsWith("project.json") && (ld.Path.IndexOf("wrap") != -1))
                    {
                        if (File.Exists(ld.Path))
                        {
                            var content = File.ReadAllText(ld.Path);
                            var spec = JsonConvert.DeserializeObject<Wrap>(content);
                            path = ld.Path.Substring(0, ld.Path.Length - 12) + spec.frameworks.net451.bin.assembly;
                        }
                    }
                    else
                    {
                        DirectoryInfo di = new DirectoryInfo(ld.Path);
                        path = DepresolveConsoleCommand.FindDll(di, ld.Identity.Name);
                        HandleNuspec(ld.Path, ld.Identity.Name);
                    }
                    r = MetadataReference.CreateFromFile(path);
                }
                catch (Exception e)
                {
                    try
                    {
                        string name = ld.Identity.Name;
                        string path = ld.Path;
                        string cd = path.Substring(0, path.LastIndexOf('/'));
                        DirectoryInfo di = new DirectoryInfo(cd);
                        string newpath = DepresolveConsoleCommand.FindDll(di, ld.Identity.Name);
                        HandleNuspec(cd, ld.Identity.Name);
                        r = MetadataReference.CreateFromFile(newpath);
                    }
                    catch (Exception ee)
                    {
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
                    if (!string.IsNullOrWhiteSpace(path) && (path.Substring(0, 3) != ".." + Path.DirectorySeparatorChar) && !path.Equals("file://applyprojectinfo.cs/"))
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
                    var def = Def.For(symbol: symbol, type: "class", name: symbol.Name).At(_path, node.Identifier.Span);
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
                    var def = Def.For(symbol: symbol, type: "interface", name: symbol.Name).At(_path, node.Identifier.Span);
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
                    var def = Def.For(symbol: symbol, type: "struct", name: symbol.Name).At(_path, node.Identifier.Span);
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
                    var def = Def.For(symbol: symbol, type: "enum", name: symbol.Name).At(_path, node.Identifier.Span);
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
                    var def = Def.For(symbol: symbol, type: "enum field", name: symbol.Name).At(_path, node.Identifier.Span);
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
                var def = Def.For(symbol: symbol, type: "method", name: symbol.Name).At(_path, node.Identifier.Span);
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
                var def = Def.For(symbol: symbol, type: "ctor", name: symbol.Name).At(_path, node.Identifier.Span);
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
                var def = Def.For(symbol: symbol, type: "property", name: symbol.Name).At(_path, node.Identifier.Span);

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
                var def = Def.For(symbol: symbol, type: "event", name: symbol.Name).At(_path, node.Identifier.Span);

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
                var def = Def.For(symbol: symbol, type: "param", name: symbol.Name).At(_path, node.Identifier.Span);
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
                var def = Def.For(symbol: symbol, type: "typeparam", name: symbol.Name).At(_path, node.Identifier.Span);
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
                    if (((IFieldSymbol)symbol).IsConst)
                    {
                        type = "const";
                    }
                }
                else
                {
                    goto skip;
                }

                var def = Def.For(symbol: symbol, type: type, name: symbol.Name).At(_path, node.Identifier.Span);
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
                    if (symbol.CandidateSymbols.Length > 0)
                    {
                        var definition = symbol.CandidateSymbols[0].OriginalDefinition;
                        if (definition != null)
                        {
                            _refs.Add(Tuple.Create(token, definition, _path));
                        }
                    }
                    else
                    {
                        goto skip;
                    }
                }
                else
                {
                    var definition = symbol.Symbol.OriginalDefinition;
                    if (definition != null)
                    {
                        _refs.Add(Tuple.Create(token, definition, _path));
                    }
                }
            }
      skip:
            base.VisitToken(token);
        }
    }


    class Wrap
    {
        [JsonProperty]
        public string version;

        [JsonProperty]
        public Framework frameworks;
    }

    class Framework
    {
        [JsonProperty]
        public Net451 net451;
    }

    class Net451
    {
        [JsonProperty]
        public Bin bin;
    }

    class Bin
    {
        [JsonProperty]
        public string assembly;
    }

}
