using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Dnx.Runtime;
using System;
using System.IO;

namespace Srclib.Nuget
{
  public class SourceUnit
  {
    public const string NUGET_PACKAGE = "NugetPackage";

    [JsonProperty]
    public string Name { get; set; }

    [JsonProperty]
    public string Type { get; set; } = NUGET_PACKAGE;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Repo { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string CommitID { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string[] Globs { get; set; }

    [JsonProperty]
    public string[] Files { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Dir { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DependencyInfo[] Dependencies { get; set; }

    [JsonProperty]
    public SourceUnit Data { get; set; }

    internal static SourceUnit FromProject(Project project, string root)
    {
      var su = new SourceUnit
      {
        Name = project.Name,
        Dir = Utils.GetRelativePath(project.ProjectDirectory, root),
        Files = project.Files.SourceFiles.Select(p => Utils.GetRelativePath(p, root)).OrderByDescending(p => p).ToArray(),
        Dependencies = project.Dependencies.Select(DependencyInfo.FromLibraryDependency).ToArray(),
      };

      return su;
    }

        internal static SourceUnit FromDirectory(string name, string root)
        {
            DirectoryInfo di = new DirectoryInfo(root);
            FileInfo[] sources = DepresolveConsoleCommand.FindSources(di);
            string[] files = new string[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                files[i] = Utils.GetRelativePath(sources[i].FullName, root);
            }
            files.OrderByDescending(p => p);
            var su = new SourceUnit
            {
                Name = name,
                Dir = root,
                Files = files,
                Dependencies = new DependencyInfo[0],
            };

            return su;
        }

  }
}
