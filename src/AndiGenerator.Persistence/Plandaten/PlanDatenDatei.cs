// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>
/// Eigenes Dateiformat des AndiGenerators (<c>&lt;andigenerator&gt;&lt;plan …&gt;</c>): <c>.modifications</c>, manuelle Pläne,
/// gemerkte Pläne (Original <c>TPlanData.SaveToXML</c>/<c>LoadFromXML</c>). Geschrieben wird wie im Original:
/// UTF-8 ohne BOM, XML-Deklaration + CRLF, der ganze Baum in einer Zeile, <c>state</c> als erstes Attribut,
/// leere Elemente als <c>&lt;x …/&gt;</c>. Mit erhaltener Attributreihenfolge sind die Dateien byte-gleich.
/// </summary>
public static class PlanDatenDatei
{
    private const string Deklaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";

    private static readonly string[] StatusNamen = ["dosNormal", "dosNew", "dosModified", "dosDelete"];

    /// <summary>Lädt den ersten <c>plan</c>-Knoten unter dem Wurzelelement.</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <returns>Der <c>plan</c>-Knoten.</returns>
    /// <exception cref="PlanDatenFormatException">Die Datei ist kein gültiges XML oder enthält keinen Knoten <c>plan</c>.</exception>
    public static DatenKnoten Laden(string pfad)
    {
        using FileStream strom = File.OpenRead(pfad);
        return Laden(strom);
    }

    /// <summary>Lädt den ersten <c>plan</c>-Knoten unter dem Wurzelelement.</summary>
    /// <param name="strom">Dateiinhalt.</param>
    /// <returns>Der <c>plan</c>-Knoten.</returns>
    /// <exception cref="PlanDatenFormatException">Der Inhalt ist kein gültiges XML oder enthält keinen Knoten <c>plan</c>.</exception>
    public static DatenKnoten Laden(Stream strom)
    {
        XDocument dokument;
        try
        {
            using XmlReader leser = XmlReader.Create(strom, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true });
            dokument = XDocument.Load(leser, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new PlanDatenFormatException("Die Datei ist kein gültiges XML: " + ex.Message, ex);
        }

        XElement plan = dokument.Root?.Elements("plan").FirstOrDefault()
            ?? throw new PlanDatenFormatException("Die Datei enthält keinen Knoten 'plan'.");
        return KnotenLaden(plan, eltern: null);
    }

    /// <summary>Erzeugt den Dateiinhalt (UTF-8 ohne BOM).</summary>
    /// <param name="plan">Wurzelknoten <c>plan</c>.</param>
    /// <returns>Der Dateiinhalt.</returns>
    public static byte[] ErzeugenBytes(DatenKnoten plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var text = new StringBuilder();
        text.Append(Deklaration).Append("\r\n<andigenerator>");
        KnotenSchreiben(text, plan);
        text.Append("</andigenerator>\r\n");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }

    /// <summary>Schreibt die Datei.</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <param name="plan">Wurzelknoten <c>plan</c>.</param>
    public static void Speichern(string pfad, DatenKnoten plan) => File.WriteAllBytes(pfad, ErzeugenBytes(plan));

    private static DatenKnoten KnotenLaden(XElement element, DatenKnoten? eltern)
    {
        var knoten = new DatenKnoten(element.Name.LocalName, eltern);
        foreach (XAttribute attribut in element.Attributes())
        {
            if (attribut.IsNamespaceDeclaration)
            {
                continue;
            }

            if (attribut.Name.LocalName == "state")
            {
                int index = Array.IndexOf(StatusNamen, attribut.Value);
                if (index >= 0)
                {
                    knoten.Status = (KnotenStatus)index; // unbekannte Werte werden wie im Original ignoriert
                }
            }
            else
            {
                knoten.Setzen(attribut.Name.LocalName, attribut.Value);
            }
        }

        foreach (XElement kind in element.Elements())
        {
            KnotenLaden(kind, knoten);
        }

        return knoten;
    }

    private static void KnotenSchreiben(StringBuilder text, DatenKnoten knoten)
    {
        text.Append('<').Append(knoten.Name);
        if (knoten.Status != KnotenStatus.Normal)
        {
            text.Append(" state=\"").Append(StatusNamen[(int)knoten.Status]).Append('"');
        }

        foreach (KeyValuePair<string, string> a in knoten.Attribute)
        {
            text.Append(' ').Append(a.Key).Append("=\"");
            Maskieren(text, a.Value);
            text.Append('"');
        }

        if (knoten.Kinder.Count == 0)
        {
            text.Append("/>");
            return;
        }

        text.Append('>');
        foreach (DatenKnoten kind in knoten.Kinder)
        {
            KnotenSchreiben(text, kind);
        }

        text.Append("</").Append(knoten.Name).Append('>');
    }

    private static void Maskieren(StringBuilder text, string wert)
    {
        foreach (char z in wert)
        {
            switch (z)
            {
                case '&': text.Append("&amp;"); break;
                case '<': text.Append("&lt;"); break;
                case '>': text.Append("&gt;"); break;
                case '"': text.Append("&quot;"); break;
                case '\r': text.Append("&#xD;"); break;
                case '\n': text.Append("&#xA;"); break;
                case '\t': text.Append("&#x9;"); break;
                default: text.Append(z); break;
            }
        }
    }
}
