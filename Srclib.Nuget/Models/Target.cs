using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Srclib.Nuget
{
  public class Target
  {
    [JsonProperty("ToRepoCloneURL")]
    public string RepoCloneURL { get; set; } = "";

    [JsonProperty("ToUnit")]
    public string Unit { get; set; }

    [JsonProperty("ToUnitType")]
    public string UnitType { get; set; } = SourceUnit.NUGET_PACKAGE;

    [JsonProperty("ToVersionString")]
    public string VersionString { get; set; } = "";

    [JsonProperty("ToRevSpec")]
    public string RevSpec { get; set; } = "";
  }
}
