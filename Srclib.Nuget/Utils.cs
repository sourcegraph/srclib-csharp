using System;
using System.Net;
using System.IO;

namespace Srclib.Nuget
{
  static class Utils
  {
    public static string GetRelativePath(string filespec, string folder)
    {
      //try
      //{
          //Uri qq = new Uri("file:///etc/fstab");
          //Console.Error.WriteLine("qq");
          filespec = "file://" + filespec;
          folder = "file://" + folder;
          //Console.Error.WriteLine("filespec=" + filespec + " folder=" + folder);
          Uri pathUri = new Uri(filespec);
          //Console.Error.WriteLine("15");
          // Folders must end in a slash
          if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
          {
            //Console.Error.WriteLine("17");
            folder += Path.DirectorySeparatorChar;
            //Console.Error.WriteLine("19");
          }
          //Console.Error.WriteLine("21");
          Uri folderUri = new Uri(folder);
          //Console.Error.WriteLine("23");
          String s = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
          //Console.Error.WriteLine(s);
          return s;
      //}
      //catch (Exception e)
      //{
      //    Console.Error.WriteLine(String.Concat("Uri exception for filespec ", filespec));
      //    return null;
      //}
    }
  }

  // TODO: Remove, for testing purposes only
  static class Utils<TInner>
  {
    static void SomeMethod<TOuter>(TInner inner, TOuter outer) { }
  }
}
