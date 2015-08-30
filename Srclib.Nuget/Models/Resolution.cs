using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using System.IO;

namespace Srclib.Nuget
{
  public class Resolution
  {
    const string REPO_FILE_NAME = "repo.json";

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
        var url = "";
        var commit = "";
        var repoFile = Path.Combine(lib.Path, REPO_FILE_NAME);
        if (File.Exists(repoFile))
        {
          var content = File.ReadAllText(repoFile);
          var spec = JsonConvert.DeserializeObject<RepoJson>(content);
          if (spec.Url.Contains("github"))
          {
            url = spec.Url;
            commit = spec.Commit;
          }
        }

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
            VersionString = lib.Identity.Version.ToString(),
            RepoCloneURL = url,
            RevSpec = commit
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

  public class RepoJson
  {
    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("commit")]
    public string Commit { get; set; }
  }
}
