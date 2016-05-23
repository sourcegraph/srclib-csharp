using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using Srclib.Nuget.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Srclib.Nuget
{
  public class Output
  {
    [JsonProperty]
    public List<Def> Defs { get; set; } = new List<Def>();

    [JsonProperty]
    public List<Ref> Refs { get; set; } = new List<Ref>();

    [JsonProperty]
    public List<Doc> Docs { get; set; } = new List<Doc>();
  }

  public class Def
  {
    /// <summary>
    /// DefKey is the natural unique key for a def. It is stable
    /// (subsequent runs of a grapher will emit the same defs with the same
    /// DefKeys).
    /// </summary>
    [JsonProperty("Path")]
    public string DefKey { get; set; }

    /// <summary>
    /// Name of the definition. This need not be unique.
    /// </summary>
    [JsonProperty]
    public string Name { get; set; }

    /// <summary>
    /// Kind is the kind of thing this definition is. This is
    /// language-specific. Possible values include "type", "func",
    /// "var", etc.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Kind { get; set; }

    [JsonProperty]
    public string File { get; set; }

    [JsonProperty]
    public UInt32 DefStart { get; set; }

    [JsonProperty]
    public UInt32 DefEnd { get; set; }

    /// <summary>
    /// External
    /// </summary>
    [JsonProperty]
    public bool External { get; set; } = false;

    /// <summary>
    /// Exported is whether this def is part of a source unit's
    /// public API. For example, in Java a "public" field is
    /// Exported.
    /// </summary>
    [JsonProperty]
    public bool Exported { get; set; } = false;

    /// <summary>
    /// Local is whether this def is local to a function or some
    /// other inner scope. Local defs do *not* have module,
    /// package, or file scope. For example, in Java a function's
    /// args are Local, but fields with "private" scope are not
    /// Local.
    /// </summary>
    [JsonProperty]
    public bool Local { get; set; } = false;

    /// <summary>
    /// Test is whether this def is defined in test code (as opposed to main
    /// code). For example, definitions in Go *_test.go files have Test = true.
    /// </summary>
    [JsonProperty]
    public bool Test { get; set; } = false;

    /// <summary>
    /// TreePath is a structurally significant path descriptor for a def. For
    /// many languages, it may be identical or similar to DefKey.Path.
    /// </summary>
    /// <remarks>
    /// A tree-path has the following restraints:
    /// 
    /// A tree-path is a chain of '/'-delimited components. A component is either a
    /// def name or a ghost component.
    /// <list type="bullet">
    ///   <item><description>A def name satifies the regex [^/-][^/]*</description></item>
    ///   <item><description>A ghost component satisfies the regex -[^/]*</description></item>
    /// </list>
    /// Any prefix of a tree-path that terminates in a def name must be a valid
    /// tree-path for some def.
    /// 
    /// The following regex captures the children of a tree-path X: X(/-[^/]*)*(/[^/-][^/]*).
    /// </remarks>
    [JsonProperty]
    public string TreePath { get; set; }

    [JsonProperty]
    public DefData Data { get; set; }

    internal static Def For(ISymbol symbol, string type, string name)
    {
      var key = symbol.GetGraphKey();

      return new Def
      {
        DefKey = key.Key,
        TreePath = key.Path,
        Kind = type,
        Name = name,
        Data = new DefData
        {
          FmtStrings = DefFormatStrings.From(symbol, key, type)
        }
      };
    }

    internal Def At(string file, TextSpan span)
    {
      File = file;
      DefStart = (uint)span.Start;
      DefEnd = (uint)span.End;
      return this;
    }
  }

  public class Ref
  {
    /// <summary>
    /// DefRepo is the repository URI of the Def that this Ref refers to.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string DefRepo { get; set; } = null;

    /// <summary>
    /// DefUnitType is the source unit type of the Def that this Ref refers to.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string DefUnitType { get; set; } = null;

    /// <summary>
    /// DefUnit is the name of the source unit that this ref exists in.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string DefUnit { get; set; } = null;

    /// <summary>
    /// Path is the path of the Def that this ref refers to.
    /// </summary>
    [JsonProperty]
    public string DefPath { get; set; }

    /// <summary>
    /// Repo is the VCS repository in which this ref exists.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Repo { get; set; } = null;

    /// <summary>
    /// CommitID is the ID of the VCS commit that this ref exists
    /// in. The CommitID is always a full commit ID (40 hexadecimal
    /// characters for git and hg), never a branch or tag name.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string CommitID { get; set; } = null;

    /// <summary>
    /// UnitType is the type name of the source unit that this ref
    /// exists in.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string UnitType { get; set; } = null;

    /// <summary>
    /// Unit is the name of the source unit that this ref exists in.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Unit { get; set; } = null;

    /// <summary>
    /// Def is true if this Ref spans the name of the Def it points to.
    /// </summary>
    [JsonProperty]
    public bool Def { get; set; } = false;

    /// <summary>
    /// File is the filename in which this Ref exists.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string File { get; set; }

    /// <summary>
    /// Start is the byte offset of this ref's first byte in File.
    /// </summary>
    [JsonProperty]
    public UInt32 Start { get; set; }

    /// <summary>
    /// End is the byte offset of this ref's last byte in File.
    /// </summary>
    [JsonProperty]
    public UInt32 End { get; set; }

    internal static Ref To(ISymbol symbol)
    {
      var key = symbol.GetGraphKey();

      return new Ref
      {
        DefPath = key.Key
      };
    }

    internal Ref At(string file, TextSpan span)
    {
      File = file;
      Start = (uint)span.Start;
      End = (uint)span.End;
      return this;
    }

    internal static Ref AtDef(Def def)
    {
      return new Ref
      {
        DefPath = def.DefKey,
        File = def.File,
        Start = def.DefStart,
        End = def.DefEnd,
        Def = true
      };
    }
  }

  public class Doc
  {
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string UnitType { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Unit { get; set; }

    [JsonProperty]
    public string Path { get; set; }

    /// <summary>
    /// Format is the the MIME-type that the documentation is stored
    /// in. Valid formats include 'text/html', 'text/plain',
    /// 'text/x-markdown', text/x-rst'.
    /// </summary>
    [JsonProperty]
    public string Format { get; set; }

    /// <summary>
    /// Data is the actual documentation text.
    /// </summary>
    [JsonProperty]
    public string Data { get; set; }

    /// <summary>
    /// File is the filename where this Doc exists.
    /// </summary>
    [JsonProperty]
    public string File { get; set; }

    /// <summary>
    /// Start is the byte offset of this Doc's first byte in File.
    /// </summary>
    [JsonProperty]
    public uint Start { get; set; }

    /// <summary>
    /// End is the byte offset of this Doc's last byte in File.
    /// </summary>
    [JsonProperty]
    public uint End { get; set; }
  }

  public class DefData
  {
    [JsonProperty]
    public DefFormatStrings FmtStrings { get; set; }
  }

  public class DefFormatStrings
  {
    [JsonProperty]
    public QualFormatStrings Name { get; set; }

    [JsonProperty]
    public QualFormatStrings Type { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string NameAndTypeSeparator { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Language { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string DefKeyword { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Kind { get; set; }

    static SymbolDisplayFormat Unqualified { get; } =
      new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
          SymbolDisplayMemberOptions.IncludeParameters |
          SymbolDisplayMemberOptions.IncludeType,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        parameterOptions:
          SymbolDisplayParameterOptions.IncludeName |
          SymbolDisplayParameterOptions.IncludeType |
          SymbolDisplayParameterOptions.IncludeParamsRefOut |
          SymbolDisplayParameterOptions.IncludeExtensionThis |
          SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        localOptions: SymbolDisplayLocalOptions.IncludeType,
        miscellaneousOptions:
          SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
          SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    static SymbolDisplayFormat ScopeQualified { get; } =
      new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
          SymbolDisplayMemberOptions.IncludeParameters |
          SymbolDisplayMemberOptions.IncludeType |
          SymbolDisplayMemberOptions.IncludeContainingType,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        parameterOptions:
          SymbolDisplayParameterOptions.IncludeName |
          SymbolDisplayParameterOptions.IncludeType |
          SymbolDisplayParameterOptions.IncludeParamsRefOut |
          SymbolDisplayParameterOptions.IncludeExtensionThis |
          SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        localOptions: SymbolDisplayLocalOptions.IncludeType,
        miscellaneousOptions:
          SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
          SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    static SymbolDisplayFormat DepQualified { get; } =
      new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
          SymbolDisplayMemberOptions.IncludeParameters |
          SymbolDisplayMemberOptions.IncludeType |
          SymbolDisplayMemberOptions.IncludeContainingType,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        parameterOptions:
          SymbolDisplayParameterOptions.IncludeName |
          SymbolDisplayParameterOptions.IncludeType |
          SymbolDisplayParameterOptions.IncludeParamsRefOut |
          SymbolDisplayParameterOptions.IncludeExtensionThis |
          SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        localOptions: SymbolDisplayLocalOptions.IncludeType,
        miscellaneousOptions:
          SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
          SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    static SymbolDisplayFormat RepositoryWideQualified { get; } =
      DepQualified;

    static SymbolDisplayFormat LanguageWideQualified { get; } =
      RepositoryWideQualified;

    internal static DefFormatStrings From(ISymbol symbol, SymbolExtensions.KeyData key, string typeString)
    {
      var name = new QualFormatStrings
      {
        Unqualified = symbol.ToDisplayString(Unqualified),
        ScopeQualified = symbol.ToDisplayString(ScopeQualified),
        DepQualified = symbol.ToDisplayString(DepQualified),
        RepositoryWideQualified = symbol.ToDisplayString(RepositoryWideQualified),
        LanguageWideQualified = symbol.ToDisplayString(LanguageWideQualified)
      };

      var type = QualFormatStrings.Single(typeString);

      return new DefFormatStrings
      {
        Name = name,
        Type = type,
        NameAndTypeSeparator = " ",
        Language = "C#",
        DefKeyword = "",
        Kind = typeString
      };
    }
  }

  public class QualFormatStrings
  {
    /// <summary>
    /// An Unqualified name is just the def's name.
    ///
    /// Examples:
    ///
    ///   Go method         `MyMethod`
    ///   Python method     `my_method`
    ///   JavaScript method `myMethod`
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Unqualified { get; set; }

    /// <summary>
    /// A ScopeQualified name is the language-specific description of the
    /// def's defining scope plus the def's unqualified name. It should
    /// uniquely describe the def among all other defs defined in the same
    /// logical package (but this is not strictly defined or enforced).
    ///
    /// Examples:
    ///
    ///   Go method         `(*MyType).MyMethod`
    ///   Python method     `MyClass.my_method`
    ///   JavaScript method `MyConstructor.prototype.myMethod`
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string ScopeQualified { get; set; }

    /// <summary>
    /// A DepQualified name is the package/module name (as seen by an external
    /// library that imports/depends on the def's package/module) plus the
    /// def's scope-qualified name. If there are nested packages, it should
    /// describe enough of the package hierarchy to distinguish it from other
    /// similarly named defs (but this is not strictly defined or enforced).
    ///
    /// Examples:
    ///
    ///   Go method       `(*mypkg.MyType).MyMethod`
    ///   Python method   `mypkg.MyClass.my_method`
    ///   CommonJS method `mymodule.MyConstructor.prototype.myMethod`
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string DepQualified { get; set; }

    /// <summary>
    /// A RepositoryWideQualified name is the full package/module name(s) plus
    /// the def's scope-qualified name. It should describe enough of the
    /// package hierarchy so that it is unique in its repository.
    /// RepositoryWideQualified differs from DepQualified in that the former
    /// includes the full nested package/module path from the repository root
    /// (e.g., 'a/b.C' for a Go func C in the repository 'github.com/user/a'
    /// subdirectory 'b'), while DepQualified would only be the last directory
    /// component (e.g., 'b.C' in that example).
    ///
    /// Examples:
    ///
    ///   Go method       `(*mypkg/subpkg.MyType).MyMethod`
    ///   Python method   `mypkg.subpkg.MyClass.my_method` (unless mypkg =~ subpkg)
    ///   CommonJS method `mypkg.mymodule.MyConstructor.prototype.myMethod` (unless mypkg =~ mymodule)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string RepositoryWideQualified { get; set; }

    /// <summary>
    /// A LanguageWideQualified name is the library/repository name plus the
    /// package-qualified def name. It should describe the def so that it
    /// is logically unique among all defs that could reasonably exist for the
    /// language that the def is written in (but this is not strictly defined
    /// or enforced).
    ///
    /// Examples:
    ///
    ///   Go method       `(*github.com/user/repo/mypkg.MyType).MyMethod`
    ///   Python method   `mylib.MyClass.my_method` (if mylib =~ mypkg, as for Django, etc.)
    ///   CommonJS method `mylib.MyConstructor.prototype.myMethod` (if mylib =~ mymod, as for caolan/async, etc.)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string LanguageWideQualified { get; set; }

    internal static QualFormatStrings Single(string val)
    {
      return new QualFormatStrings
      {
        Unqualified = val,
        ScopeQualified = val,
        DepQualified = val,
        RepositoryWideQualified = val,
        LanguageWideQualified = val
      };
    }
  }
}
