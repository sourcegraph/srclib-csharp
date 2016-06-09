using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;
using System.Linq;

namespace Srclib.Nuget
{
  class ScanConsoleCommand
  {

        private static PackageVersions pv = new PackageVersions();

        /// <summary>
        /// Convert .csproj files into DNX configuration files 
        /// </summary>
        /// <param name="path">path to project folder</param>
        public static void ConvertCsproj(string path)
        {
            HashSet<string> names = new HashSet<string>();
            string dir = path.Substring(0, path.LastIndexOf('/'));
            if (!File.Exists(dir + "/project.json"))
            {
                //gac works correctly only in docker image
                string gac = "/gac/v4.5/";
                string global = "";
                string input = File.ReadAllText(path);
                string output = "{\n  \"frameworks\": {\n    \"dnx451\": {\n      \"dependencies\": {\n";
                int idx = input.IndexOf("<Reference Include=\"");
                while (idx != -1)
                {
                    int quote = input.IndexOf('\"', idx + 20);
                    int comma = input.IndexOf(',', idx + 20);
                    if ((comma != -1) && (comma < quote))
                    {
                        quote = comma;
                    }
                    string name = input.Substring(idx + 20, quote - idx - 20);
                    if ((name.IndexOf('$') == -1) && (name.IndexOf('\\') == -1) && !names.Contains(name))
                    {
                        names.Add(name);
                        string version = pv.GetVersion(name.ToLower());
                        if (version != null)
                        {
                            output = output + "          \"" + name + "\": \"" + version + "\",\n";
                        }
                        else
                        {
                            output = output + "          \"" + name + "\": \"\",\n";
                            if (File.Exists(gac + name + ".dll") && !name.Equals("mscorlib"))
                            {
                                global = global + gac + name + ".dll\n";
                            }
                        }
                    }
                    idx = input.IndexOf("<Reference Include=\"", quote);
                }
                output = output + "      }\n    }\n  }\n}";
                File.WriteAllText(dir + "/project.json", output);
                if (global.Length > 0)
                {
                    File.WriteAllText(dir + "/global.log", global);
                }
            }
        }

        /// <summary>
        /// finds all c# Visual Studio project files in project folder and subfolders
        /// </summary>
        /// <param name="root">directory info for project folder</param>
        /// <returns>returns an array of csproj files</returns>
        public static FileInfo[] FindVSProjects(DirectoryInfo root)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;

            try
            {
                files = root.GetFiles("*.csproj");
            }
            catch (Exception e)
            {
            }

            subDirs = root.GetDirectories();
            foreach (DirectoryInfo dirInfo in subDirs)
            {
                // Resursive call for each subdirectory.
                FileInfo[] res = FindVSProjects(dirInfo);
                files = files.Concat(res).ToArray();
            }
            return files;
        }

        private static string getGitUrl()
        {
            try
            {
                string config = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "./.git/config"));
                int i = config.IndexOf("\turl = ");
                if (i != -1)
                {
                    int j = config.IndexOf('\n', i);
                    string url = config.Substring(i + 7, j - i - 7);
                    return url;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }


    public static void Register(CommandLineApplication cmdApp, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnvironment)
    {
      cmdApp.Command("scan", c => {
        c.Description = "Scan a directory tree and produce a JSON array of source units";

        c.HelpOption("-?|-h|--help");

        c.OnExecute(async () => {

            string url = getGitUrl();
            if (url.EndsWith("github.com/dotnet/coreclr"))
            {
                Console.Error.WriteLine("special case coreclr");
                DepresolveConsoleCommand.RunForResult("/bin/bash", "-c \"rm `find -name project.json`\"");
                DepresolveConsoleCommand.RunForResult("/bin/bash", "-c \"rm `find -name '*.csproj'`\"");
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./src/mscorlib/project.json"), "{\n  \"frameworks\": {\n    \"dnx451\": {\n      \"dependencies\": {\n      }\n    }\n  }\n}");
            }
            else if (url.EndsWith("github.com/Microsoft/referencesource"))
            {
                Console.Error.WriteLine("special case referencesource");
                string json = "{\n  \"frameworks\": {\n    \"dnx451\": {\n      \"dependencies\": {\n      }\n    }\n  }\n}";

                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./mscorlib/project.json"), json);

                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Activities/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Xml.Linq/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Numerics/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.Services/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.IdentityModel/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl/System.IO.v2.5/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl/System.Runtime.v1.5/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl/System.IO.v1.5/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl/System.Threading.Tasks.v2.5/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl/System.Threading.Tasks.v1.5/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl/System.Runtime.v2.5/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Activities.Presentation/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Activities/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.ApplicationServices/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./XamlBuildTask/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.IdentityModel.Selectors/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Workflow.Runtime/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.Extensions/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.Mobile/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Activation/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Net/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Xml/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Channels/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Data.SqlXml/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Data.DataSetExtensions/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Discovery/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Activities.DurableInstancing/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Routing/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Core/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Workflow.ComponentModel/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.Entity.Design/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Data.Entity.Design/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ComponentModel.DataAnnotations/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Runtime.DurableInstancing/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.DynamicData/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Internals/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Data/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./SMDiagnostics/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.WorkflowServices/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Workflow.Activities/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.WasHosting/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.Entity/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Data.Entity/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Configuration/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Web.Routing/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Runtime.Serialization/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Runtime.Caching/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Xaml.Hosting/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.ServiceModel.Web/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Activities.Core.Presentation/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./System.Data.Linq/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl.Async/Microsoft.Threading.Tasks.Extensions.Desktop/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl.Async/Microsoft.Threading.Tasks.Extensions.Phone/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl.Async/Microsoft.Threading.Tasks.Extensions/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl.Async/Microsoft.Threading.Tasks/project.json"), json);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "./Microsoft.Bcl.Async/Microsoft.Threading.Tasks.Extensions.Silverlight/project.json"), json);
            }
            else if (url.EndsWith("github.com/AutoMapper/AutoMapper"))
            {
                DepresolveConsoleCommand.RunForResult("/bin/bash", @"-c ""sed 's/..\\\\/..\//g' -i `find -name project.json`""");
            }


          var dir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "."));

          DirectoryInfo di = new DirectoryInfo(Directory.GetCurrentDirectory());
          FileInfo[] fis = FindVSProjects(di);
          string[] projects = new string[fis.Length];
          for (int i = 0; i < fis.Length; i++)
          {
              projects[i] = fis[i].FullName;
              ConvertCsproj(projects[i]);
          }


          var sourceUnits = new List<SourceUnit>();
          foreach(var proj in Scan(dir))
          {
            sourceUnits.Add(SourceUnit.FromProject(proj, dir));
          }

          if (sourceUnits.Count == 0)
          {
            //not a DNX project and not a VS project
            sourceUnits.Add(SourceUnit.FromDirectory("name", dir));
          }

          Console.WriteLine(JsonConvert.SerializeObject(sourceUnits, Formatting.Indented));

          await DepresolveConsoleCommand.RunResolve(dir);

          return 0;
        });
      });
    }

    static IEnumerable<Project> Scan(string dir)
    {
      if (Project.HasProjectFile(dir))
      {
        Project proj;
        if (Project.TryGetProject(dir, out proj) && proj.CompilerServices == null)
          yield return proj;
        yield break;
      }

      foreach (var subdir in Directory.EnumerateDirectories(dir))
      {
        // Skip directories starting with .
        if (subdir.StartsWith("."))
          continue;

        foreach (var proj in Scan(subdir))
          yield return proj;
      }
    }
  }
}
