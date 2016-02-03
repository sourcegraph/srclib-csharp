using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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
    /// <summary>
    /// Process a type (class or struct or interface) declaration for documentation.
    /// </summary>
    /// <param name="symbol">The type in question.</param>
    /// <returns><c>Docs</c> if the type contains any, otherwise <c>null</c>.</returns>
    public static Doc ForClass(INamedTypeSymbol symbol)
    {
      try 
      {
        var doc = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(doc))
        {
          return null;
        }

        var sections = new List<Tuple<int, string, string>>();
        var xdoc = XDocument.Parse(doc).Root;

        ProcessFull(xdoc, sections);

        if (sections.Count == 0)
          return null;

        var resultString = String.Join("\n", sections.Select(t => $"<h{t.Item1 + 2}>{t.Item2}</h{t.Item1 + 2}>{t.Item3}"));

        return new Doc
        {
          Format = "text/html",
          Data = resultString
        };
      }
      catch (Exception e)
      {
        return null;
      }
    }

    /// <summary>
    /// Process a method declaration for documentation.
    /// </summary>
    /// <param name="symbol">The method in question.</param>
    /// <returns><c>Docs</c> if the method contains any, otherwise <c>null</c>.</returns>
    public static Doc ForMethod(IMethodSymbol symbol)
    {
      try 
      {
        var doc = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(doc))
        {
          return null;
        }

        var sections = new List<Tuple<int, string, string>>();
        var xdoc = XDocument.Parse(doc).Root;

        ProcessFull(xdoc, sections);
        var cursor = sections.FindIndex(t => t.Item2 == "Summary");
        var paramsSection = ProcessParameters(xdoc, symbol.Parameters.Select(p => p.Name).ToList());
        sections.Insert(cursor + 1, paramsSection);

        var returnElement = xdoc.Element("returns");
        if (returnElement != null)
        {
          var content = ProcessContent(returnElement);
          if (!string.IsNullOrEmpty(content))
          {
            sections.Insert(cursor + 2, Tuple.Create(2, "Return value", $"<p>{content}</p>"));
          }
        }

        var resultString = string.Join("\n", sections.Select(t => $"<h{t.Item1 + 2}>{t.Item2}</h{t.Item1 + 2}>{t.Item3}"));

        return new Doc
        {
          Format = "text/html",
          Data = resultString
        };
      }
      catch (Exception e)
      {
        return null;
      }
    }

    public static Tuple<int, string, string> ProcessParameters(XElement doc, IList<string> names)
    {
      var sb = new StringBuilder();
      using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
      {
        writer.WriteStartElement("dl");
        foreach (var name in names)
        {
          writer.WriteStartElement("dt");
          writer.WriteStartElement("em");
          writer.WriteString(name);
          writer.WriteEndElement();
          writer.WriteEndElement();

          writer.WriteStartElement("dd");
          var node = doc.Elements("param").Where(e => e.Attribute("name").Value == name).FirstOrDefault();
          if (node != null)
          {
            ProcessContent(node, writer);
          }
          else
          {
            writer.WriteStartElement("span");
            writer.WriteAttributeString("class", "empty-param");
            writer.WriteString("No documentation found.");
            writer.WriteEndElement();
          }
          writer.WriteEndElement();
        }
        writer.WriteEndElement();
      }

      return Tuple.Create(2, "Parameters", sb.ToString());
    }

    static void ProcessFull(XElement doc, List<Tuple<int, string, string>> sections)
    {
      var summary = doc.Element("summary");
      var remarks = doc.Element("remarks");

      if (summary != null)
      {
        var text = ProcessContent(summary);
        if (!string.IsNullOrEmpty(text))
          sections.Add(Tuple.Create(1, "Summary", $"<p>{text}</p>"));
      }

      if (remarks != null)
      {
        var text = ProcessContent(remarks);
        if (!string.IsNullOrEmpty(text))
          sections.Add(Tuple.Create(1, "Remarks", $"<p>{text}</p>"));
      }
    }

    static string ProcessContent(XElement node)
    {
      var sb = new StringBuilder();
      using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
      {
        var notEmpty = ProcessContent(node, writer);
        if (!notEmpty)
        {
          return null;
        }
      }

      return sb.ToString();
    }

    static bool ProcessContent(XElement node, XmlWriter writer)
    {
      bool written = false;
      using (var reader = node.CreateReader())
      {
        while (!reader.EOF)
        {
          switch (reader.NodeType)
          {
            case XmlNodeType.Text:
              written = true;
              writer.WriteString(reader.Value);
              break;

            case XmlNodeType.Element:
              switch (reader.LocalName)
              {
                case "see":
                  written = true;
                  var cref = reader.GetAttribute("cref");
                  writer.WriteStartElement("span");

                  //TODO($Alxandr): lookup actual symbol.
                  writer.WriteAttributeString("data-cref", cref);
                  writer.WriteString(cref);
                  writer.WriteEndElement();
                  break;

                case "c":
                  written = true;
                  writer.WriteStartElement("strong");
                  break;
              }

              break;

            case XmlNodeType.EndElement:
              switch (reader.LocalName)
              {
                case "c":
                  writer.WriteEndElement();
                  break;
              }

              break;
          }

          reader.Read();
        }
      }

      return written;
    }
  }
}
