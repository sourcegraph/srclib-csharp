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


    public static void Register(CommandLineApplication cmdApp, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnvironment)
    {
      cmdApp.Command("scan", c => {
        c.Description = "Scan a directory tree and produce a JSON array of source units";

        c.HelpOption("-?|-h|--help");

        c.OnExecute(async () => {
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
