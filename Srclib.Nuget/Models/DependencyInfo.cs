using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Dnx.Runtime;

namespace Srclib.Nuget
{
  public class DependencyInfo
  {
    [JsonProperty]
    public string Name { get; set; }

    [JsonProperty]
    public string Version { get; set; }

    public static DependencyInfo FromLibraryDependency(LibraryDependency lib)
    {
      return new DependencyInfo {
        Name = lib.Name,
        Version = lib.LibraryRange.ToString()
      };
    }
  }
}
