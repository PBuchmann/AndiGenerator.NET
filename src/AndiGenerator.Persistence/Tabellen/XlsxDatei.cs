// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace AndiGenerator.Persistence.Tabellen;

/// <summary>
/// Schreibt eine Arbeitsmappe als Excel-Datei (<c>.xlsx</c>, Office Open XML) ohne Fremdbibliothek: Texte als
/// Inline-Zeichenketten, Datum und Uhrzeit als Excel-Seriennummern mit passendem Format, Fettschrift, Farben,
/// Spaltenbreiten nach Inhalt sowie stehende Kopfzeile mit Filter.
/// </summary>
public static class XlsxDatei
{
    /// <summary>Höchstlänge eines Blattnamens in Excel.</summary>
    public const int MaxBlattname = 31;

    private const string Haupt = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Beziehung = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Paket = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string Inhaltstypen = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string Typ = "application/vnd.openxmlformats-officedocument.spreadsheetml.";
    private const int FormatDatum = 164;
    private const int FormatUhrzeit = 165;
    private const int FormatGanzzahl = 3;

    private static readonly char[] Verboten = ['[', ']', ':', '*', '?', '/', '\\'];

    /// <summary>Speichert die Arbeitsmappe.</summary>
    /// <param name="pfad">Pfad der Datei.</param>
    /// <param name="mappe">Die Arbeitsmappe.</param>
    public static void Speichern(string pfad, Arbeitsmappe mappe)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfad);
        using FileStream datei = File.Create(pfad);
        Schreiben(datei, mappe);
    }

    /// <summary>Schreibt die Arbeitsmappe in einen Datenstrom.</summary>
    /// <param name="ziel">Der Datenstrom; er bleibt offen.</param>
    /// <param name="mappe">Die Arbeitsmappe.</param>
    public static void Schreiben(Stream ziel, Arbeitsmappe mappe)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(mappe);
        IReadOnlyList<string> namen = Blattnamen(mappe.Blaetter.Select(b => b.Name).ToList());
        var stile = new Stilliste();
        using var zip = new ZipArchive(ziel, ZipArchiveMode.Create, leaveOpen: true);
        for (int i = 0; i < mappe.Blaetter.Count; i++)
        {
            Arbeitsblatt blatt = mappe.Blaetter[i];
            Eintrag(zip, $"xl/worksheets/sheet{i + 1}.xml", w => Blatt(w, blatt, stile, ersteBlatt: i == 0));
        }

        Eintrag(zip, "xl/styles.xml", stile.Ausgeben);
        Eintrag(zip, "xl/workbook.xml", w => Mappe(w, namen));
        Eintrag(zip, "xl/_rels/workbook.xml.rels", w => MappenBeziehungen(w, namen.Count));
        Eintrag(zip, "_rels/.rels", w =>
        {
            w.WriteStartElement("Relationships", Paket);
            Beziehungseintrag(w, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
            w.WriteEndElement();
        });
        Eintrag(zip, "[Content_Types].xml", w => Typen(w, namen.Count));
    }

    /// <summary>Macht Blattnamen für Excel zulässig: verbotene Zeichen ersetzen, auf 31 Zeichen kürzen, eindeutig machen.</summary>
    /// <param name="namen">Die gewünschten Namen.</param>
    /// <returns>Die zulässigen Namen in derselben Reihenfolge.</returns>
    public static IReadOnlyList<string> Blattnamen(IReadOnlyList<string> namen)
    {
        ArgumentNullException.ThrowIfNull(namen);
        var vergeben = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ergebnis = new List<string>();
        for (int i = 0; i < namen.Count; i++)
        {
            string name = string.Concat(namen[i].Select(z => Verboten.Contains(z) ? '_' : z)).Trim().Trim('\'');
            if (name.Length == 0)
            {
                name = "Blatt " + (i + 1).ToString(CultureInfo.InvariantCulture);
            }

            name = Kuerzen(name, MaxBlattname);
            string kandidat = name;
            int n = 1;
            while (!vergeben.Add(kandidat))
            {
                n++;
                string zusatz = " (" + n.ToString(CultureInfo.InvariantCulture) + ")";
                kandidat = Kuerzen(name, MaxBlattname - zusatz.Length) + zusatz;
            }

            ergebnis.Add(kandidat);
        }

        return ergebnis;
    }

    /// <summary>Spaltenbezeichnung wie in Excel.</summary>
    /// <param name="spalte">Spalte ab 0.</param>
    /// <returns>z. B. <c>A</c>, <c>Z</c>, <c>AA</c>.</returns>
    public static string Spaltenname(int spalte)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spalte);
        var name = new StringBuilder();
        for (int n = spalte + 1; n > 0; n = (n - 1) / 26)
        {
            name.Insert(0, (char)('A' + ((n - 1) % 26)));
        }

        return name.ToString();
    }

    private static string Kuerzen(string text, int laenge) => text.Length <= laenge ? text : text[..laenge].TrimEnd();

    private static void Eintrag(ZipArchive zip, string name, Action<XmlWriter> schreiben)
    {
        ZipArchiveEntry eintrag = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream strom = eintrag.Open();
        using var w = XmlWriter.Create(strom, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        w.WriteStartDocument(true);
        schreiben(w);
        w.WriteEndDocument();
    }

    private static void Beziehungseintrag(XmlWriter w, string id, string typ, string ziel)
    {
        w.WriteStartElement("Relationship", Paket);
        w.WriteAttributeString("Id", id);
        w.WriteAttributeString("Type", typ);
        w.WriteAttributeString("Target", ziel);
        w.WriteEndElement();
    }

    private static void Typen(XmlWriter w, int blaetter)
    {
        w.WriteStartElement("Types", Inhaltstypen);
        Standardtyp(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        Standardtyp(w, "xml", "application/xml");
        Teiltyp(w, "/xl/workbook.xml", Typ + "sheet.main+xml");
        Teiltyp(w, "/xl/styles.xml", Typ + "styles+xml");
        for (int i = 1; i <= blaetter; i++)
        {
            Teiltyp(w, $"/xl/worksheets/sheet{i}.xml", Typ + "worksheet+xml");
        }

        w.WriteEndElement();
    }

    private static void Standardtyp(XmlWriter w, string endung, string typ)
    {
        w.WriteStartElement("Default", Inhaltstypen);
        w.WriteAttributeString("Extension", endung);
        w.WriteAttributeString("ContentType", typ);
        w.WriteEndElement();
    }

    private static void Teiltyp(XmlWriter w, string teil, string typ)
    {
        w.WriteStartElement("Override", Inhaltstypen);
        w.WriteAttributeString("PartName", teil);
        w.WriteAttributeString("ContentType", typ);
        w.WriteEndElement();
    }

    private static void Mappe(XmlWriter w, IReadOnlyList<string> namen)
    {
        w.WriteStartElement("workbook", Haupt);
        w.WriteAttributeString("xmlns", "r", null, Beziehung);
        w.WriteStartElement("sheets", Haupt);
        for (int i = 0; i < namen.Count; i++)
        {
            w.WriteStartElement("sheet", Haupt);
            w.WriteAttributeString("name", namen[i]);
            w.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("id", Beziehung, "rId" + (i + 1).ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }

        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void MappenBeziehungen(XmlWriter w, int blaetter)
    {
        w.WriteStartElement("Relationships", Paket);
        for (int i = 1; i <= blaetter; i++)
        {
            string nummer = i.ToString(CultureInfo.InvariantCulture);
            Beziehungseintrag(w, "rId" + nummer, Beziehung + "/worksheet", "worksheets/sheet" + nummer + ".xml");
        }

        Beziehungseintrag(w, "rId" + (blaetter + 1).ToString(CultureInfo.InvariantCulture), Beziehung + "/styles", "styles.xml");
        w.WriteEndElement();
    }

    private static void Blatt(XmlWriter w, Arbeitsblatt blatt, Stilliste stile, bool ersteBlatt)
    {
        int spalten = blatt.Zeilen.Select(z => z.Count).DefaultIfEmpty(0).Max();
        w.WriteStartElement("worksheet", Haupt);
        w.WriteAttributeString("xmlns", "r", null, Beziehung);
        w.WriteStartElement("sheetViews", Haupt);
        w.WriteStartElement("sheetView", Haupt);
        if (ersteBlatt)
        {
            w.WriteAttributeString("tabSelected", "1");
        }

        w.WriteAttributeString("workbookViewId", "0");
        if (blatt.Kopfzeile is int kopf)
        {
            string zeilen = (kopf + 1).ToString(CultureInfo.InvariantCulture);
            w.WriteStartElement("pane", Haupt);
            w.WriteAttributeString("ySplit", zeilen);
            w.WriteAttributeString("topLeftCell", "A" + (kopf + 2).ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("activePane", "bottomLeft");
            w.WriteAttributeString("state", "frozen");
            w.WriteEndElement();
        }

        w.WriteEndElement();
        w.WriteEndElement();
        Spaltenbreiten(w, blatt, spalten);
        w.WriteStartElement("sheetData", Haupt);
        for (int z = 0; z < blatt.Zeilen.Count; z++)
        {
            Zeile(w, blatt.Zeilen[z], z, stile);
        }

        w.WriteEndElement();
        if (blatt.Kopfzeile is int k && spalten > 0)
        {
            w.WriteStartElement("autoFilter", Haupt);
            w.WriteAttributeString("ref", $"A{k + 1}:{Spaltenname(spalten - 1)}{Math.Max(blatt.Zeilen.Count, k + 1)}");
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void Spaltenbreiten(XmlWriter w, Arbeitsblatt blatt, int spalten)
    {
        if (spalten == 0)
        {
            return;
        }

        w.WriteStartElement("cols", Haupt);
        for (int s = 0; s < spalten; s++)
        {
            int spalte = s;
            int zeichen = blatt.Zeilen.Where(z => spalte < z.Count).Select(z => Anzeigelaenge(z[spalte])).DefaultIfEmpty(0).Max();
            double breite = Math.Clamp((zeichen * 1.1) + 2, 6, 60);
            string nummer = (s + 1).ToString(CultureInfo.InvariantCulture);
            w.WriteStartElement("col", Haupt);
            w.WriteAttributeString("min", nummer);
            w.WriteAttributeString("max", nummer);
            w.WriteAttributeString("width", breite.ToString("0.##", CultureInfo.InvariantCulture));
            w.WriteAttributeString("customWidth", "1");
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static int Anzeigelaenge(Blattzelle zelle) => zelle.Wert switch
    {
        null => 0,
        string text => text.Split('\n').Max(t => t.Length) + (zelle.Fett ? 1 : 0),
        DateTime => 10,
        TimeSpan => 5,
        IFormattable zahl => zahl.ToString("N0", CultureInfo.GetCultureInfo("de-DE")).Length,
        _ => 10,
    };

    private static void Zeile(XmlWriter w, IReadOnlyList<Blattzelle> zellen, int zeile, Stilliste stile)
    {
        string nummer = (zeile + 1).ToString(CultureInfo.InvariantCulture);
        w.WriteStartElement("row", Haupt);
        w.WriteAttributeString("r", nummer);
        for (int s = 0; s < zellen.Count; s++)
        {
            Blattzelle zelle = zellen[s];
            if (zelle.Wert is null && zelle.Hintergrund is null)
            {
                // Leere Zellen nur schreiben, wenn sie eingefärbt sind.
                continue;
            }

            int stil = stile.Index(zelle);

            w.WriteStartElement("c", Haupt);
            w.WriteAttributeString("r", Spaltenname(s) + nummer);
            if (stil != 0)
            {
                w.WriteAttributeString("s", stil.ToString(CultureInfo.InvariantCulture));
            }

            Wert(w, zelle.Wert);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void Wert(XmlWriter w, object? wert)
    {
        string? zahl = wert switch
        {
            DateTime datum => datum.ToOADate().ToString("R", CultureInfo.InvariantCulture),
            TimeSpan zeit => zeit.TotalDays.ToString("R", CultureInfo.InvariantCulture),
            int ganz => ganz.ToString(CultureInfo.InvariantCulture),
            double komma => komma.ToString("R", CultureInfo.InvariantCulture),
            _ => null,
        };
        if (zahl is not null)
        {
            w.WriteElementString("v", Haupt, zahl);
            return;
        }

        if (wert is null)
        {
            return;
        }

        string text = Convert.ToString(wert, CultureInfo.InvariantCulture) ?? string.Empty;
        w.WriteAttributeString("t", "inlineStr");
        w.WriteStartElement("is", Haupt);
        w.WriteStartElement("t", Haupt);
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains('\n', StringComparison.Ordinal)))
        {
            w.WriteAttributeString("xml", "space", null, "preserve");
        }

        w.WriteString(text);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>Sammelt die Zellformate (Zahlenformat, Schrift, Füllung) und schreibt sie als <c>styles.xml</c>.</summary>
    private sealed class Stilliste
    {
        private readonly List<(bool Fett, int? Farbe)> schriften = [(false, null)];
        private readonly List<int?> fuellungen = [null, null];
        private readonly List<(int Format, int Schrift, int Fuellung)> stile = [(0, 0, 0)];

        public int Index(Blattzelle zelle)
        {
            int format = zelle.Format switch
            {
                Zellformat.Datum => FormatDatum,
                Zellformat.Uhrzeit => FormatUhrzeit,
                Zellformat.Ganzzahl => FormatGanzzahl,
                _ => 0,
            };
            int schrift = Hinzufuegen(schriften, (zelle.Fett, zelle.Schriftfarbe));
            int fuellung = zelle.Hintergrund is null ? 0 : Hinzufuegen(fuellungen, zelle.Hintergrund, ab: 2);
            return Hinzufuegen(stile, (format, schrift, fuellung));
        }

        public void Ausgeben(XmlWriter w)
        {
            w.WriteStartElement("styleSheet", Haupt);
            w.WriteStartElement("numFmts", Haupt);
            w.WriteAttributeString("count", "2");
            Zahlenformat(w, FormatDatum, "dd.mm.yyyy");
            Zahlenformat(w, FormatUhrzeit, "hh:mm");
            w.WriteEndElement();

            Liste(w, "fonts", schriften, s =>
            {
                w.WriteStartElement("font", Haupt);
                if (s.Fett)
                {
                    w.WriteStartElement("b", Haupt);
                    w.WriteEndElement();
                }

                Wertelement(w, "sz", "11");
                if (s.Farbe is int farbe)
                {
                    w.WriteStartElement("color", Haupt);
                    w.WriteAttributeString("rgb", Argb(farbe));
                    w.WriteEndElement();
                }

                Wertelement(w, "name", "Calibri");
                w.WriteEndElement();
            });

            w.WriteStartElement("fills", Haupt);
            w.WriteAttributeString("count", fuellungen.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < fuellungen.Count; i++)
            {
                Fuellung(w, i, fuellungen[i]);
            }

            w.WriteEndElement();

            w.WriteStartElement("borders", Haupt);
            w.WriteAttributeString("count", "1");
            w.WriteStartElement("border", Haupt);
            foreach (string seite in new[] { "left", "right", "top", "bottom", "diagonal" })
            {
                w.WriteStartElement(seite, Haupt);
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("cellStyleXfs", Haupt);
            w.WriteAttributeString("count", "1");
            Formatsatz(w, (0, 0, 0), mitVerweis: false);
            w.WriteEndElement();

            Liste(w, "cellXfs", stile, s => Formatsatz(w, s, mitVerweis: true));

            w.WriteStartElement("cellStyles", Haupt);
            w.WriteAttributeString("count", "1");
            w.WriteStartElement("cellStyle", Haupt);
            w.WriteAttributeString("name", "Standard");
            w.WriteAttributeString("xfId", "0");
            w.WriteAttributeString("builtinId", "0");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        private static int Hinzufuegen<T>(List<T> liste, T wert, int ab = 0)
        {
            int index = liste.FindIndex(ab, e => EqualityComparer<T>.Default.Equals(e, wert));
            if (index >= 0)
            {
                return index;
            }

            liste.Add(wert);
            return liste.Count - 1;
        }

        private static string Argb(int farbe) => "FF" + (farbe & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

        private static void Wertelement(XmlWriter w, string name, string wert)
        {
            w.WriteStartElement(name, Haupt);
            w.WriteAttributeString("val", wert);
            w.WriteEndElement();
        }

        private static void Zahlenformat(XmlWriter w, int id, string code)
        {
            w.WriteStartElement("numFmt", Haupt);
            w.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("formatCode", code);
            w.WriteEndElement();
        }

        private static void Fuellung(XmlWriter w, int index, int? farbe)
        {
            w.WriteStartElement("fill", Haupt);
            w.WriteStartElement("patternFill", Haupt);
            if (index == 1)
            {
                w.WriteAttributeString("patternType", "gray125");
            }
            else if (farbe is int f)
            {
                w.WriteAttributeString("patternType", "solid");
                w.WriteStartElement("fgColor", Haupt);
                w.WriteAttributeString("rgb", Argb(f));
                w.WriteEndElement();
                w.WriteStartElement("bgColor", Haupt);
                w.WriteAttributeString("indexed", "64");
                w.WriteEndElement();
            }
            else
            {
                w.WriteAttributeString("patternType", "none");
            }

            w.WriteEndElement();
            w.WriteEndElement();
        }

        private static void Formatsatz(XmlWriter w, (int Format, int Schrift, int Fuellung) stil, bool mitVerweis)
        {
            w.WriteStartElement("xf", Haupt);
            w.WriteAttributeString("numFmtId", stil.Format.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("fontId", stil.Schrift.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("fillId", stil.Fuellung.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("borderId", "0");
            if (mitVerweis)
            {
                w.WriteAttributeString("xfId", "0");
                if (stil.Format != 0)
                {
                    w.WriteAttributeString("applyNumberFormat", "1");
                }

                if (stil.Schrift != 0)
                {
                    w.WriteAttributeString("applyFont", "1");
                }

                if (stil.Fuellung != 0)
                {
                    w.WriteAttributeString("applyFill", "1");
                }
            }

            w.WriteEndElement();
        }

        private static void Liste<T>(XmlWriter w, string name, List<T> eintraege, Action<T> schreiben)
        {
            w.WriteStartElement(name, Haupt);
            w.WriteAttributeString("count", eintraege.Count.ToString(CultureInfo.InvariantCulture));
            foreach (T eintrag in eintraege)
            {
                schreiben(eintrag);
            }

            w.WriteEndElement();
        }
    }
}
