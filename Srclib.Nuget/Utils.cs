using System;
using System.Net;
using System.IO;

namespace Srclib.Nuget
{
  static class Utils
  {
    /// <summary>
    /// gets a relative filepath from folder to filespec
    /// </summary>
    /// <param name="folder">base path</param>
    /// <param name="filepec">destination path</param>
    public static string GetRelativePath(string filespec, string folder)
    {
      filespec = "file://" + filespec;
      folder = "file://" + folder;
      Uri pathUri = new Uri(filespec);
      // Folders must end in a slash
      if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
      {
        folder += Path.DirectorySeparatorChar;
      }
      Uri folderUri = new Uri(folder);
      String s = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
      return s;
    }
  }
}
