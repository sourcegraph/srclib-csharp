using System;
using System.Net;
using System.IO;

namespace Srclib.Nuget
{
  static class Utils
  {
    public static string GetRelativePath(string filespec, string folder)
    {
      Uri pathUri = new Uri(filespec);
      // Folders must end in a slash
      if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
      {
        folder += Path.DirectorySeparatorChar;
      }
      Uri folderUri = new Uri(folder);
      return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }
  }
}
