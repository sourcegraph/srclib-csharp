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

    // TODO: Data, docs

    internal static Def For(ISymbol symbol, string type, string name)
    {
      var key = symbol.GetGraphKey();

      return new Def
      {
        DefKey = key.Key,
        TreePath = key.Path,
        Kind = type,
        Name = name
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

  }
}
