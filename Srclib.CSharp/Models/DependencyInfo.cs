using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Dnx.Runtime;

namespace Srclib.CSharp
{
  public class DependencyInfo
  {
    [JsonProperty]
    public string Name { get; set; }
    
    [JsonProperty]
    public string Range { get; set; }
    
    [JsonProperty]
    public string Identity { get; set; }
    
    public static DependencyInfo FromLibraryDependency(LibraryDependency lib)
    {
      return new DependencyInfo {
        Name = lib.Name,
        Range = lib.LibraryRange.ToString(),
        Identity = lib.Library?.ToString()
      };
    }
  }
}