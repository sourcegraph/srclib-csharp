using Microsoft.Dnx.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Srclib.Nuget
{
  public static class LibraryUtils
  {
    public static IEqualityComparer<LibraryDescription> Comparer => new LibraryComparer();

    class LibraryComparer : IEqualityComparer<LibraryDescription>
    {
      public bool Equals(LibraryDescription x, LibraryDescription y)
      {
        if (x == null && y == null)
          return true;

        if (x.Identity == null || y.Identity == null)
          return false;

        return x.Identity.Name == y.Identity.Name &&
          x.Identity.Version == y.Identity.Version;
      }

      public int GetHashCode(LibraryDescription obj)
      {
        if (obj == null) return 0;

        if (obj.Identity == null)
          return 0;

        return obj.Identity.Name.GetHashCode() ^ obj.Identity.Version.GetHashCode();
      }
    }
  }
}
