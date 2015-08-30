using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;

namespace Srclib.Nuget
{
  public class Resolution
  {
    [JsonProperty]
    public ResolutionIdentity Raw { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Target ResolvedTarget { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Error { get; set; }

    internal static Resolution FromLibrary(LibraryDescription lib)
    {
      if (lib.Identity != null)
      {
        return new Resolution
        {
          Raw = new ResolutionIdentity
          {
            Name = lib.Identity.Name,
            Version = lib.Identity.Version.ToString()
          },
          ResolvedTarget = new Target
          {
            Unit = lib.Identity.Name,
            VersionString = lib.Identity.Version.ToString()
          }
        };
      }
      else
      {
        return new Resolution
        {
          Raw = new ResolutionIdentity
          {
            Name = lib.RequestedRange.Name,
            Version = lib.RequestedRange.VersionRange.ToString()
          },
          Error = "Dependensy resolution failed"
        };
      }
    }
  }

  public class ResolutionIdentity
  {
    [JsonProperty]
    public string Name { get; set; }

    [JsonProperty]
    public string Version { get; set; }
  }
}
