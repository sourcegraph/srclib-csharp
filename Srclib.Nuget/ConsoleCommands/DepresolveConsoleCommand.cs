using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Srclib.Nuget
{
    class DepresolveConsoleCommand
    {
        static Lazy<string> _dnuPath;

        /// <summary>
        /// Initialization method for depresolve process
        /// </summary>
        /// <param name="cmdApp">application to run (srclib-csharp)</param>
        /// <param name="appEnv">common application information</param>
        /// <param name="runtimeEnv">environment representation</param>
        public static void Register(CommandLineApplication cmdApp, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv, Microsoft.Extensions.PlatformAbstractions.IRuntimeEnvironment runtimeEnv)
        {
            _dnuPath = new Lazy<string>(FindDnuNix);

            cmdApp.Command("depresolve", c => 
            {
                c.Description = "Perform a combination of parsing, static analysis, semantic analysis, and type inference";

                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () => 
                {
                    var jsonIn = await Console.In.ReadToEndAsync();
                    var sourceUnit = JsonConvert.DeserializeObject<SourceUnit>(jsonIn);
                    var dir = Path.Combine(Directory.GetCurrentDirectory(), sourceUnit.Dir);
                    var deps = DepResolve(dir);
                    var result = new List<Resolution>();
                    foreach (var dep in deps)
                    {
                        result.Add(Resolution.FromLibrary(dep));
                    }
                    Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
                    return 0;
                });
            });
        }

        static IEnumerable<LibraryDescription> DepResolve(string dir)
        {
            Project proj;
            if(!Project.TryGetProject(dir, out proj))
            {
                //not a DNX project
                List<LibraryDescription> empty = new List<LibraryDescription>();
                return empty;
            }

            var allDeps = GetAllDeps(proj);
            return allDeps;
        }


        public static IEnumerable<LibraryDescription> DepResolve(Project proj)
        {
            var allDeps = GetAllDeps(proj);
            return allDeps;
        }


        static IEnumerable<LibraryDescription> GetAllDeps(Project proj) =>
            proj.GetTargetFrameworks().Select(f => f.FrameworkName)
            .SelectMany(f =>
            {
                var context = new ApplicationHostContext
                {
                    Project = proj,
                    TargetFramework = f
                };
                IList<LibraryDescription> libs = null;
                while (libs == null) {
                    try
                    {
                        libs = ApplicationHostContext.GetRuntimeLibraries(context);
                    }
                    catch (Exception e)
                    {
                    }
                }
                // the first library description is always self-reference, so skip it
                return libs.Skip(1);
            })
            .Distinct(LibraryUtils.Comparer)
            .OrderBy(l => l.Identity?.Name);


        public static async Task RunResolve(string dir)
        {
            var p = new Process();
            p.StartInfo.WorkingDirectory = dir;
            p.StartInfo.FileName = _dnuPath.Value;
            p.StartInfo.Arguments = "restore";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            // it's important to read stdout and stderr, else it might deadlock
            var outs = await Task.WhenAll(p.StandardOutput.ReadToEndAsync(), p.StandardError.ReadToEndAsync());
            p.WaitForExit();
        }

        static string FindDnuNix()
        {
            return RunForResult("/bin/bash", "-c \"which dnu\"");
        }

        public static string RunForResult(string shell, string command)
        {
            var p = new Process();
            p.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
            p.StartInfo.FileName = shell;
            p.StartInfo.Arguments = command;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = false;

            p.Start();
            var result = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return result;
        }

        public static FileInfo[] FindSources(DirectoryInfo root)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;

            try
            {
                files = root.GetFiles("*.cs");
            }
            catch (Exception e)
            {
            }

            subDirs = root.GetDirectories();
            foreach (DirectoryInfo dirInfo in subDirs)
            {
                // Resursive call for each subdirectory.
                FileInfo[] res = FindSources(dirInfo);
                files = files.Concat(res).ToArray();
            }
            return files;
        }

        public static string FindDll(DirectoryInfo root, string name)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;

            try
            {
                files = root.GetFiles("*.dll");
            }
            catch (Exception e)
            {
            }

            if (files != null)
            {
                foreach (FileInfo fi in files)
                {
                    if (fi.Name.Equals(name + ".dll"))
                    {
                        return fi.FullName;
                    }
                }
            }

            subDirs = root.GetDirectories();
            foreach (DirectoryInfo dirInfo in subDirs)
            {
                // Resursive call for each subdirectory.
                string res = FindDll(dirInfo, name);
                if (res != null)
                {
                    return res;
                }
            }
            return null;
        }

        public static string FindNuspec(DirectoryInfo root)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;

            try
            {
                files = root.GetFiles("*.nuspec");
            }
            catch (Exception e)
            {
            }

            if (files != null)
            {
                return files[0].FullName;
            }

            subDirs = root.GetDirectories();
            foreach (DirectoryInfo dirInfo in subDirs)
            {
                // Resursive call for each subdirectory.
                string res = FindNuspec(dirInfo);
                if (res != null)
                {
                    return res;
                }
            }
            return null;
        }

    }
}
