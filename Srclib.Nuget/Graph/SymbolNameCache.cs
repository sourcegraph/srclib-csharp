using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Srclib.Nuget.Graph
{
  static class SymbolNameCache
  {
    static ImmutableDictionary<ISymbol, int> _cache = ImmutableDictionary.Create<ISymbol, int>();
    static ImmutableDictionary<string, int> _nextId = ImmutableDictionary.Create<string, int>();

    /// <summary>
    /// generate a unique for local variable
    /// </summary>
    /// <param name="symbol">semantic info element representing local variable</param>
    public static string GenerateUniqueName(this ISymbol symbol, string kind)
    {
      var nextId = ImmutableInterlocked.AddOrUpdate(ref _nextId, kind, 0, (_, old) => old + 1);
      var id = ImmutableInterlocked.GetOrAdd(ref _cache, symbol, nextId);
      return $"{kind}${id}";
    }
  }
}
