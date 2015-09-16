using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Srclib.Nuget.Documentation
{
  /// <summary>
  /// Utility class to deal with xml documentation.
  /// </summary>
  /// <remarks>
  /// These remarks are just here to test that it works.
  /// </remarks>
  public static class DocProcessor
  {
    public static Doc ForClass(INamedTypeSymbol symbol)
    {
      var doc = symbol.GetDocumentationCommentXml();
      if (string.IsNullOrEmpty(doc))
      {
        return null;
      }

      var result = new List<Tuple<string, string>>();
      using (var sr = new StringReader($"{doc}"))
      using (var reader = XmlReader.Create(sr))
      {
        // move to <member>
        reader.MoveToContent();

        // move to content
        reader.Read();

        ProcessFull(reader, result);
      }

      if (result.Count == 0)
        return null;

      var resultString = String.Join("\n", result.Select(t => $"<h1>{t.Item1}</h1>{t.Item2}"));

      return new Doc
      {
        Format = "text/html",
        Data = resultString
      };
    }

    static void ProcessFull(XmlReader reader, List<Tuple<string, string>> sections)
    {
      var summary = new StringBuilder();
      var remarks = new StringBuilder();

      while (!reader.EOF) {
        switch (reader.LocalName)
        {
          case "summary":
            ProcessContent(reader.ReadSubtree(), summary);
            break;

          case "remarks":
            ProcessContent(reader.ReadSubtree(), remarks);
            break;
        }

        reader.Read();
      }

      if (summary.Length > 0)
      {
        sections.Add(Tuple.Create("Summary", summary.ToString()));
      }

      if (remarks.Length > 0)
      {
        sections.Add(Tuple.Create("Remarks", remarks.ToString()));
      }
    }

    static void ProcessContent(XmlReader reader, StringBuilder builder)
    {
      reader.MoveToContent();
      reader.Read();
      var started = false;

      using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
      {
        while (!reader.EOF)
        {
          switch(reader.NodeType)
          {
            case XmlNodeType.Text:
              if (!started)
              {
                started = true;
                writer.WriteStartElement("p");
              }

              writer.WriteString(reader.Value);
              break;

            case XmlNodeType.Element:
              switch(reader.LocalName)
              {
                case "see":
                  if (!started)
                  {
                    started = true;
                    writer.WriteStartElement("span");
                  }

                  var cref = reader.GetAttribute("cref");
                  writer.WriteStartElement("span");

                  // todo, lookup actual symbol.
                  writer.WriteAttributeString("data-cref", cref);
                  writer.WriteString(cref);
                  writer.WriteEndElement();
                  break;

              }

              break;
          }

          reader.Read();
        }

        if (started)
        {
          writer.WriteEndElement();
        }
      }
    }
  }
}
