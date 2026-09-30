// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using AndiGenerator.Persistence.Tabellen;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Excel-Export ohne Fremdbibliothek: Aufbau der Datei, Zellwerte, Formate und Blattnamen.</summary>
public sealed class XlsxDateiTests
{
    private static readonly XNamespace Haupt = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Arbeitsmappe_enthaelt_alle_Teile_und_Blaetter()
    {
        var anpfiff = new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Unspecified);
        List<IReadOnlyList<Blattzelle>> zeilen =
        [
            [new Blattzelle("Datum", Fett: true, Hintergrund: 0xE8E8E8), Blattzelle.Text("Uhrzeit", fett: true), Blattzelle.Text("Heim", fett: true)],
            [Blattzelle.Datum(anpfiff), Blattzelle.Uhrzeit(anpfiff), Blattzelle.Text(" Tüs & <Co> ")],
            [Blattzelle.Datum(null), Blattzelle.Leer, Blattzelle.Zahl(1234), new Blattzelle(2.5), new Blattzelle(null, Hintergrund: 0xFF0000)],
        ];
        var liste = new Arbeitsblatt("Spielplan", zeilen, Kopfzeile: 0);
        var mappe = new Arbeitsmappe([liste, new Arbeitsblatt("Leer", [])]);

        using var strom = new MemoryStream();
        XlsxDatei.Schreiben(strom, mappe);
        strom.Position = 0;
        using var zip = new ZipArchive(strom, ZipArchiveMode.Read);

        string[] erwartet = ["[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml"];
        Assert.Equal(erwartet.Order(StringComparer.Ordinal), zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        Assert.Equal(["Spielplan", "Leer"], Lesen(zip, "xl/workbook.xml").Descendants(Haupt + "sheet").Select(s => (string)s.Attribute("name")!));

        XDocument blatt = Lesen(zip, "xl/worksheets/sheet1.xml");
        List<XElement> zellen = blatt.Descendants(Haupt + "c").ToList();
        XElement datum = zellen.Single(c => (string?)c.Attribute("r") == "A2");
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified).ToOADate(), double.Parse(datum.Element(Haupt + "v")!.Value, CultureInfo.InvariantCulture));
        XElement zeit = zellen.Single(c => (string?)c.Attribute("r") == "B2");
        Assert.Equal(20.0 / 24, double.Parse(zeit.Element(Haupt + "v")!.Value, CultureInfo.InvariantCulture), 9);
        XElement text = zellen.Single(c => (string?)c.Attribute("r") == "C2");
        Assert.Equal("inlineStr", (string?)text.Attribute("t"));
        Assert.Equal(" Tüs & <Co> ", text.Descendants(Haupt + "t").Single().Value);
        Assert.DoesNotContain(zellen, c => (string?)c.Attribute("r") is "A3" or "B3");
        Assert.Equal("1234", zellen.Single(c => (string?)c.Attribute("r") == "C3").Element(Haupt + "v")!.Value);
        Assert.Equal("2.5", zellen.Single(c => (string?)c.Attribute("r") == "D3").Element(Haupt + "v")!.Value);
        Assert.NotNull(zellen.Single(c => (string?)c.Attribute("r") == "E3").Attribute("s"));
        Assert.Equal("A1:E3", (string?)blatt.Descendants(Haupt + "autoFilter").Single().Attribute("ref"));
        Assert.Equal("frozen", (string?)blatt.Descendants(Haupt + "pane").Single().Attribute("state"));
        Assert.Equal(5, blatt.Descendants(Haupt + "col").Count());

        XDocument stile = Lesen(zip, "xl/styles.xml");
        int formate = stile.Descendants(Haupt + "cellXfs").Single().Elements().Count();
        Assert.Equal(formate, int.Parse((string)stile.Descendants(Haupt + "cellXfs").Single().Attribute("count")!, CultureInfo.InvariantCulture));
        Assert.All(zellen.Where(c => c.Attribute("s") is not null), c => Assert.InRange(int.Parse((string)c.Attribute("s")!, CultureInfo.InvariantCulture), 1, formate - 1));
        Assert.Contains(stile.Descendants(Haupt + "fgColor"), f => (string?)f.Attribute("rgb") == "FFFF0000");
        Assert.Contains(stile.Descendants(Haupt + "numFmt"), f => (string?)f.Attribute("formatCode") == "dd.mm.yyyy");

        Assert.Empty(Lesen(zip, "xl/worksheets/sheet2.xml").Descendants(Haupt + "c"));
    }

    [Fact]
    public void Blattnamen_werden_fuer_Excel_zulaessig()
    {
        IReadOnlyList<string> namen = XlsxDatei.Blattnamen(["Kosten", "kosten", "A/B:C?", string.Empty, new string('x', 40), new string('x', 40)]);

        Assert.Equal("Kosten", namen[0]);
        Assert.Equal("kosten (2)", namen[1]);
        Assert.Equal("A_B_C_", namen[2]);
        Assert.Equal("Blatt 4", namen[3]);
        Assert.Equal(new string('x', 31), namen[4]);
        Assert.Equal(new string('x', 27) + " (2)", namen[5]);
    }

    [Fact]
    public void Spaltennamen_wie_in_Excel()
    {
        Assert.Equal("A", XlsxDatei.Spaltenname(0));
        Assert.Equal("Z", XlsxDatei.Spaltenname(25));
        Assert.Equal("AA", XlsxDatei.Spaltenname(26));
        Assert.Equal("AZ", XlsxDatei.Spaltenname(51));
        Assert.Equal("BA", XlsxDatei.Spaltenname(52));
        Assert.Throws<ArgumentOutOfRangeException>(() => XlsxDatei.Spaltenname(-1));
    }

    [Fact]
    public void Speichern_schreibt_eine_Datei()
    {
        string pfad = Path.Combine(Path.GetTempPath(), "andigen-xlsx-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            XlsxDatei.Speichern(pfad, new Arbeitsmappe([new Arbeitsblatt("A", [[Blattzelle.Text("x")]])]));
            using ZipArchive zip = ZipFile.OpenRead(pfad);
            Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    private static XDocument Lesen(ZipArchive zip, string name)
    {
        using Stream strom = zip.GetEntry(name)!.Open();
        return XDocument.Load(strom);
    }
}
