using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Srclib.Nuget.Graph
{
  static class SymbolExtensions
  {
    public struct KeyData
    {
      public string Key { get; }
      public string Path { get; }
      internal string Sep { get; }

      public KeyData(string key, string path, string sep)
      {
        Key = key;
        Path = path;
        Sep = sep;
      }

      internal static KeyData Empty { get; } = new KeyData(null, null, null);
      internal static KeyData FromKey(string key) => new KeyData(key, null, null);
      internal static KeyData Append(KeyData key, Segment segment) =>
        new KeyData(key.Key + key.Sep + segment.Name, string.IsNullOrEmpty(key.Path) ? segment.Name : key.Path + "/" + segment.Name, segment.Separator);
    }

    internal struct Segment
    {
      public string Name { get; }
      public string Separator { get; }

      private Segment(string name, string sep)
      {
        Name = name;
        Separator = sep;
      }

      public static Segment From(ISymbol symbol, string separator)
      {
        return new Segment(GetSymbolName(symbol), separator);
      }
    }

    public static KeyData GetGraphKey(this ISymbol symbol)
    {
      var key = GetGraphKeyImpl(symbol);
      if (string.IsNullOrEmpty(key.Key))
        throw new InvalidOperationException("Key cannot be empty");
      if (string.IsNullOrEmpty(key.Path))
        throw new InvalidOperationException("Path cannot be empty");

      return key;
    }

    static KeyData GetGraphKeyImpl(ISymbol symbol,
      bool allowTypeParameter = false)
    {
      if (allowTypeParameter && symbol is ITypeParameterSymbol)
      {
        return KeyData.FromKey(GetSymbolName(symbol));
      }

      var segments = GetGraphKeyImpl(symbol, ImmutableList.Create<Segment>());
      return segments.Aggregate(KeyData.Empty, KeyData.Append);
    }

    static IEnumerable<Segment> GetGraphKeyImpl(ISymbol symbol, IImmutableList<Segment> acc)
    {
      if (symbol == null)
        return acc.Reverse();

      if (symbol is ITypeSymbol)
        return GetGraphKeyImpl(symbol.ContainingSymbol, acc.Add(Segment.From(symbol, "+")));

      if (symbol is INamespaceSymbol)
      {
        if (((INamespaceSymbol)symbol).IsGlobalNamespace)
          return GetGraphKeyImpl(symbol.ContainingSymbol, acc);

        return GetGraphKeyImpl(symbol.ContainingSymbol, acc.Add(Segment.From(symbol, ".")));
      }

      if (symbol is IModuleSymbol)
        return GetGraphKeyImpl(symbol.ContainingSymbol, acc);

      if (symbol is IAssemblySymbol)
        return GetGraphKeyImpl(symbol.ContainingSymbol, acc);

      return GetGraphKeyImpl(symbol.ContainingSymbol, acc.Add(Segment.From(symbol, "!")));
    }

    static string GetSymbolName(ISymbol symbol)
    {
      var method = symbol as IMethodSymbol;
      if (method != null)
      {
        var name = method.MetadataName;
        if (method.MethodKind == MethodKind.LambdaMethod)
        {
          name = method.GenerateUniqueName("-lambda");
        }
        else if (method.MethodKind == MethodKind.Conversion)
        {
          string returnTypeName;
          if (method.ReturnType is INamedTypeSymbol)
          {
            var t = method.ReturnType as INamedTypeSymbol;
            if (t.TypeArguments.Length > 0)
            {
              returnTypeName = $"{t.MetadataName}<{string.Join(",", t.TypeArguments.Select(p => GetGraphKeyImpl(p, allowTypeParameter: true).Key))}>";
            }
            else
            {
              returnTypeName = t.MetadataName;
            }
          }
          else
          {
            returnTypeName = method.ReturnType.MetadataName;
          }
          name = returnTypeName + "=" + name;
        }

        if (method.IsGenericMethod)
        {
          name = name + "`" + method.TypeParameters.Length;
        }

        return $"{name}({string.Join(";", method.Parameters.Select(p => GetGraphKeyImpl(p.Type, allowTypeParameter: true).Key))})";
      }

      if (symbol is INamedTypeSymbol)
      {
        var type = symbol as INamedTypeSymbol;
        var name = type.MetadataName;
        if (type.TypeArguments.Length > 0)
        {
          return $"{name}<{string.Join(",", type.TypeArguments.Select(p => GetGraphKeyImpl(p, allowTypeParameter: true).Key))}>";
        }
        else
        {
          return symbol.MetadataName;
        }
      }

      if (symbol is IArrayTypeSymbol)
      {
        var s = symbol as IArrayTypeSymbol;
        string underlying = GetSymbolName(s.ElementType);
        int rank = s.Rank;
        string res = underlying + "[";
        for (int i = 0; i < rank - 1; i++)
        {
          res = res + ",";
        }
        res = res + "]";
        return res;
      }

      var local = symbol as ILocalSymbol;
      if (local != null)
      {
        // As far as I know, you can define the same named local several places within a single method body.
        return local.GenerateUniqueName(local.MetadataName);
      }

      return symbol.MetadataName;
    }

    public static bool IsExported(this ISymbol symbol)
    {
      return symbol.DeclaredAccessibility == Accessibility.Public
        || symbol.DeclaredAccessibility == Accessibility.Protected
        || symbol.DeclaredAccessibility == Accessibility.ProtectedAndInternal
        || symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
    }
  }
}
