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

        /// <summary>map from dll name into repo URL</summary>
        readonly static Dictionary<string, string> dllToProjectUrl;

        /// <summary>map from projectUrl nuspec attribute into repo URL</summary>
        readonly static Dictionary<string, string> projectUrlToRepo;

        SemanticModel _sm;
        string _path;

        static GraphRunner()
        {
            projectUrlToRepo = new Dictionary<string, string>()
            {
                { "http://www.newtonsoft.com/json", "github.com/JamesNK/Newtonsoft.Json" },
                { "http://autofac.org/", "github.com/autofac/Autofac" },
                { "http://msdn.com/roslyn", "github.com/dotnet/roslyn" },


                // multiple dlls from different repositories have www.asp.net as projectUrl
                // so we probably need a multimap here
                // TODO($ildarisaev): implement a correct mapping for all dlls
                { "http://www.asp.net/", "github.com/aspnet/dnx" }
            };

            dllToProjectUrl = new Dictionary<string, string>()
            {
                { "mscorlib.dll", "github.com/Microsoft/referencesource" },
                { "System.dll", "github.com/Microsoft/referencesource" },
                { "System.Activities.dll", "github.com/Microsoft/referencesource" },
                { "System.Activities.Core.Presentation.dll", "github.com/Microsoft/referencesource" },
                { "System.Activities.DurableInstancing.dll", "github.com/Microsoft/referencesource" },

                { "System.Activities.Presentation.dll", "github.com/Microsoft/referencesource" },
                { "System.ComponentModel.DataAnnotations.dll", "github.com/Microsoft/referencesource" },
                { "System.Configuration.dll", "github.com/Microsoft/referencesource" },
                { "System.Core.dll", "github.com/Microsoft/referencesource" },

                { "System.Data.dll", "github.com/Microsoft/referencesource" },
                { "System.Data.DataSetExtensions.dll", "github.com/Microsoft/referencesource" },
                { "System.Data.Entity.dll", "github.com/Microsoft/referencesource" },
                { "System.Data.Entity.Design.dll", "github.com/Microsoft/referencesource" },

                { "System.Data.Linq.dll", "github.com/Microsoft/referencesource" },
                { "System.Data.SqlXml.dll", "github.com/Microsoft/referencesource" },
                { "System.IdentityModel.dll", "github.com/Microsoft/referencesource" },
                { "System.IdentityModel.Selectors.dll", "github.com/Microsoft/referencesource" },

                { "System.Net.dll", "github.com/Microsoft/referencesource" },
                { "System.Numerics.dll", "github.com/Microsoft/referencesource" },
                { "System.Runtime.Caching.dll", "github.com/Microsoft/referencesource" },
                { "System.Runtime.DurableInstancing.dll", "github.com/Microsoft/referencesource" },

                { "System.Runtime.Serialization.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.Activation.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.Activities.dll", "github.com/Microsoft/referencesource" },

                { "System.ServiceModel.Channels.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.Discovery.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.Internals.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.Routing.dll", "github.com/Microsoft/referencesource" },

                { "System.ServiceModel.WasHosting.dll", "github.com/Microsoft/referencesource" },
                { "System.ServiceModel.Web.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.ApplicationServices.dll", "github.com/Microsoft/referencesource" },

                { "System.Web.DynamicData.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.Entity.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.Entity.Design.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.Extensions.dll", "github.com/Microsoft/referencesource" },

                { "System.Web.Mobile.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.Routing.dll", "github.com/Microsoft/referencesource" },
                { "System.Web.Services.dll", "github.com/Microsoft/referencesource" },
                { "System.Workflow.Activities.dll", "github.com/Microsoft/referencesource" },

                { "System.Workflow.ComponentModel.dll", "github.com/Microsoft/referencesource" },
                { "System.Workflow.Runtime.dll", "github.com/Microsoft/referencesource" },
                { "System.WorkflowServices.dll", "github.com/Microsoft/referencesource" },
                { "System.Xaml.Hosting.dll", "github.com/Microsoft/referencesource" },

                { "System.Xml.dll", "github.com/Microsoft/referencesource" },
                { "System.Xml.Linq.dll", "github.com/Microsoft/referencesource" },
                { "XamlBuildTask.dll", "github.com/Microsoft/referencesource" },
                { "SMDiagnostics.dll", "github.com/Microsoft/referencesource" }
            };
        }

        private GraphRunner() : base(SyntaxWalkerDepth.Token)
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
                    else if (!(definition is INamespaceSymbol))
                    {
                        var reference = Ref.To(definition).At(file, token.Span);
                        if ((definition.ContainingAssembly != null) && (definition.ContainingAssembly.Identity != null) && (definition.ContainingAssembly.Identity.Name != null))
                        {
                            string dll = definition.ContainingAssembly.Identity.Name + ".dll";
                            if (dllToProjectUrl.ContainsKey(dll))
                            {
                                string url = dllToProjectUrl[dll];
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

        /// <summary>
        /// Get a project URL (if present) from a dependency nuspec 
        /// </summary>
        /// <param name="path">rependency folder</param>
        /// <param name="dll">dll name</param>
        internal static void HandleNuspec(string path, string dll)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            string nuspec = DepresolveConsoleCommand.FindNuspec(di);
            if (nuspec != null)
            {
                var content = File.ReadAllText(nuspec);
                int i = content.IndexOf("<projectUrl>");
                if (i != -1)
                {
                    int j = content.IndexOf("</projectUrl>");
                    string projectUrl = content.Substring(i + 12, j - i - 12);
                    dllToProjectUrl[dll + ".dll"] = projectUrl;
                }
            }
        }

        /// <summary>
        /// Main scan method: resolve and load dependencies, run roslyn compiler, emit defs and refs
        /// </summary>
        /// <param name="context">Project context.</param>
        internal static async Task<Output> Graph(GraphContext context)
        {
            if (context.Project == null)
            {
                Project project;
                if (!Project.TryGetProject(context.ProjectDirectory, out project))
                {
                    //not a DNX project
                    DirectoryInfo di = new DirectoryInfo(context.ProjectDirectory);
                    FileInfo[] fis = DepresolveConsoleCommand.FindSources(di);
                    string[] files = new string[fis.Length];
                    for (int i = 0; i < fis.Length; i++)
                    {
                        files[i] = fis[i].FullName;
                    }
                    Microsoft.CodeAnalysis.Text.SourceText[] sources = new Microsoft.CodeAnalysis.Text.SourceText[files.Length];
                    SyntaxTree[] trees = new SyntaxTree[files.Length];
                    Dictionary<SyntaxTree, string> dict = new Dictionary<SyntaxTree, string>();
                    var compilation = CSharpCompilation.Create("name");
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            FileStream source = new FileStream(files[i], FileMode.Open);
                            sources[i] = Microsoft.CodeAnalysis.Text.SourceText.From(source);
                            trees[i] = CSharpSyntaxTree.ParseText(sources[i]);
                            if (trees[i] != null)
                            {
                                compilation = compilation.AddSyntaxTrees(trees[i]);
                                dict[trees[i]] = files[i];
                            }
                            source.Dispose();
                        }
                        catch (Exception e)
                        {
                        }
                    }

                    var gr = new GraphRunner();
                    foreach (var st in compilation.SyntaxTrees)
                    {
                        var path = dict[st];
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            path = Utils.GetRelativePath(path, context.ProjectDirectory);
                            if (!string.IsNullOrWhiteSpace(path) && (path.Substring(0, 3) != ".." + Path.DirectorySeparatorChar) && !path.Equals("file://applyprojectinfo.cs/"))
                            {
                                // this is a source code file we want to grap
                                gr._sm = compilation.GetSemanticModel(st, false);
                                gr._path = path;
                                var root = await st.GetRootAsync();
                                gr.Visit(root);
                            }
                        }
                    }
                    gr._sm = null;
                    gr._path = null;
                    gr.RunTokens();
                    return gr._output;

                }

                context.Project = project;
            }

            if (context.Project.GetTargetFrameworks().Count() == 0)
            {
                return new Output();
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

            IEnumerable<LibraryDescription> deps = DepresolveConsoleCommand.DepResolve(context.Project);
            HashSet<PortableExecutableReference> libs = new HashSet<PortableExecutableReference>();
            try
            {
                if (File.Exists(context.ProjectDirectory + "/global.log"))
                {
                    string[] ss = File.ReadAllLines(context.ProjectDirectory + "/global.log");
                    foreach (string s in ss)
                    {
                        libs.Add(MetadataReference.CreateFromFile(s));
                    }
                }
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
        /// Traverse AST node that represents namespace declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            try
            {
                var symbol = _sm.GetDeclaredSymbol(node);
                if (!_defined.Contains(symbol))
                {
                    _defined.Add(symbol);
                    var def = Def.For(symbol: symbol, type: "namespace", name: symbol.Name).At(_path, node.Name.GetLastToken().Span);
                    if (symbol.IsExported())
                    {
                        def.Exported = true;
                    }
                    AddDef(def);
                }
                base.VisitNamespaceDeclaration(node);
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents delegate declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            try
            {
                if (!node.Identifier.Span.IsEmpty)
                {
                    var symbol = _sm.GetDeclaredSymbol(node);
                    _defined.Add(symbol);
                    var def = Def.For(symbol: symbol, type: "delegate", name: symbol.Name).At(_path, node.Identifier.Span);
                    if (symbol.IsExported())
                    {
                        def.Exported = true;
                    }
                    AddDef(def, DocProcessor.ForClass(symbol));
                }
                base.VisitDelegateDeclaration(node);
            }
            catch (Exception e)
            {
            }
        }


        /// <summary>
        /// Traverse AST node that represents catch clause parameter declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            try
            {
                var symbol = _sm.GetDeclaredSymbol(node);
                if (symbol != null)
                {
                    var def = Def.For(symbol: symbol, type: "local", name: symbol.Name).At(_path, node.Identifier.Span);
                    def.Local = true;
                    def.Exported = false;
                    AddDef(def);
                }
                base.VisitCatchDeclaration(node);
            }
            catch (Exception e)
            {
            }
        }


        /// <summary>
        /// Traverse AST node that represents class declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents interface declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents struct declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }


        /// <summary>
        /// Traverse AST node that represents enumeration declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents enumeration constant declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }


        /// <summary>
        /// Traverse AST node that represents method declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents constructor declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents property declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents event declaration
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents method or constructor parameter
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitParameter(ParameterSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents type parameter (in generic declarations)
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitTypeParameter(TypeParameterSyntax node)
        {
            try
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents field and variable declarations
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            try
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
                    else if (symbol is IEventSymbol)
                    {
                        type = "field";
                        exported = symbol.IsExported();
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
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Traverse AST node that represents foreach loop and add a def for a variable declared in its header
        /// </summary>
        /// <param name="node">AST node.</param>
        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            try
            {
                var symbol =  _sm.GetDeclaredSymbol(node);
                if (symbol != null)
                {
                    var def = Def.For(symbol: symbol, type: "field", name: symbol.Name).At(_path, node.Identifier.Span);
                    def.Local = true;
                    def.Exported = false;
                    AddDef(def);
                }
                base.VisitForEachStatement(node);
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// If a token is resolved by parser, add a token to collection of resolved tokens
        /// </summary>
        /// <param name="token">token to check</param>
        public override void VisitToken(SyntaxToken token)
        {
            try
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
            catch (Exception e)
            {
            }
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
