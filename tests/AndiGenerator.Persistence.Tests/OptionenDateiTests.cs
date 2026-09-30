// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Persistence.Optionen;

namespace AndiGenerator.Persistence.Tests;

public class OptionenDateiTests
{
    private const string OptionenOrdner = "Optionen (Stand 27.09.2026 19 Uhr)";

    private const string Kopf = "<?xml version=\"1.0\" encoding=\"iso-8859-15\"?>\r\n";

    [Fact]
    public void AlleOriginaldateienWerdenByteGleichGeschrieben()
    {
        string[] dateien = Directory.GetFiles(Testdaten.Datei(OptionenOrdner), "*.options");
        Assert.True(dateien.Length >= 26, $"Nur {dateien.Length} Optionsdateien gefunden.");

        var fehler = new List<string>();
        foreach (string datei in dateien)
        {
            byte[] original = File.ReadAllBytes(datei);
            byte[] neu = OptionenDatei.ErzeugenBytes(OptionenDatei.Laden(datei));
            if (!original.AsSpan().SequenceEqual(neu))
            {
                fehler.Add(Path.GetFileName(datei));
            }
        }

        Assert.Empty(fehler);
    }

    [Fact]
    public void StandardEntsprichtZurueckgesetzterDatei()
    {
        // "Kreisoberliga 2026.options" wurde durch „Nein, Standardeinstellungen“ zurückgesetzt (Befund #34).
        byte[] original = File.ReadAllBytes(Datei("Kreisoberliga 2026.options"));
        Assert.Equal(original, OptionenDatei.ErzeugenBytes(Berechnungsoptionen.Standard));
    }

    [Fact]
    public void VerbandsligaRheinlandSuedWestHatErwarteteWerte()
    {
        Berechnungsoptionen o = OptionenDatei.Laden(Datei("Verbandsliga Rheinland Süd-West 2026.options"));

        Assert.True(o.FreitagZaehltZumWochenende);
        Assert.Equal(Gewichtung.SehrWenig, o.Spieltag);
        Assert.Equal(Gewichtung.SehrWenig, o.SpieltagUeberlappung);
        Assert.Equal(Gewichtung.ExtremHoch, o.LetzterSpieltag);
        Assert.Equal(Gewichtung.Normal, o.LetzterSpieltagUeberlappung);
        Assert.Equal(Gewichtung.NichtBeruecksichtigen, o.Fuer(MannschaftsKostenart.DreiTageAbstand));
        Assert.Equal(Gewichtung.Hoch, o.Fuer(MannschaftsKostenart.Sperrtermine));
        Assert.Equal(Gewichtung.SehrHoch, o.Fuer(MannschaftsKostenart.Pflichtspieltage));
        Assert.Equal(Rundenplanung.Beide, o.Rundenplanung);
        Assert.False(o.Doppelrunde);
        Assert.True(o.AutomatischeMitte);

        Assert.Equal(new[] { "3082601", "3082571", "3082582", "3082676" }, o.Mannschaften.Select(m => m.MannschaftsId));
        MannschaftsGewichtung erste = o.Mannschaften[0];
        Assert.Equal(Gewichtung.Normal, erste.Gesamt);
        Assert.Equal(Gewichtung.NichtBeruecksichtigen, erste.JeKostenart[(int)MannschaftsKostenart.ParalleleSpiele]);
        Assert.Equal(Gewichtung.NichtBeruecksichtigen, erste.JeKostenart[(int)MannschaftsKostenart.UngleichHeimAuswaerts]);
    }

    [Fact]
    public void KreisligaGruppeBHatAchtMannschaftseintraege()
    {
        Berechnungsoptionen o = OptionenDatei.Laden(Datei("Kreisliga Gruppe B 2026.options"));
        Assert.Equal(8, o.Mannschaften.Count);
    }

    [Fact]
    public void FehlendeDateiLiefertStandard()
    {
        string pfad = Path.Combine(Path.GetTempPath(), "gibt-es-nicht-" + Guid.NewGuid().ToString("N"), "AndiGenerator.options");
        Assert.Same(Berechnungsoptionen.Standard, OptionenDatei.Laden(pfad));
    }

    [Fact]
    public void UngueltigesXmlLiefertStandard()
    {
        Assert.Same(Berechnungsoptionen.Standard, AusText("<andigenerator-options><weighting"));
    }

    [Fact]
    public void FehlendesFreitagsattributBedeutetFalseFehlenderKnotenTrue()
    {
        Assert.False(AusText(Kopf + "<andigenerator-options><weighting/></andigenerator-options>").FreitagZaehltZumWochenende);
        Assert.True(AusText(Kopf + "<andigenerator-options/>").FreitagZaehltZumWochenende);
        Assert.False(AusText(Kopf + "<andigenerator-options><weighting friday-is-part-of-weekend=\"True\"/></andigenerator-options>").FreitagZaehltZumWochenende);
    }

    [Fact]
    public void WerteOhneGrossKleinschreibungUnbekannteAlsNormal()
    {
        Berechnungsoptionen o = AusText(Kopf +
            "<andigenerator-options><weighting gameday=\"COHOCH\" last-gameday=\"coGibtEsNicht\" mktRanking=\"coignore\"/>" +
            "<round-planing planing=\"RPCORONA\"/></andigenerator-options>");
        Assert.Equal(Gewichtung.Hoch, o.Spieltag);
        Assert.Equal(Gewichtung.Normal, o.LetzterSpieltag);
        Assert.Equal(Gewichtung.NichtBeruecksichtigen, o.Fuer(MannschaftsKostenart.Setzliste));
        Assert.Equal(Rundenplanung.Corona, o.Rundenplanung);
    }

    [Fact]
    public void UnbekannteRundenplanungWirdBeide()
    {
        Assert.Equal(Rundenplanung.Beide, AusText(Kopf + "<andigenerator-options><round-planing planing=\"rpX\"/></andigenerator-options>").Rundenplanung);
    }

    [Fact]
    public void MannschaftOhneIdWirdIgnoriertDoppelteIdUeberschreibt()
    {
        Berechnungsoptionen o = AusText(Kopf +
            "<andigenerator-options><weighting>" +
            "<team teamid=\"1\" main=\"coHoch\"/><team main=\"coWenig\"/><team teamid=\"2\"/><team teamid=\"1\" main=\"coWenig\"/>" +
            "</weighting></andigenerator-options>");
        Assert.Equal(new[] { "1", "2" }, o.Mannschaften.Select(m => m.MannschaftsId));
        Assert.Equal(Gewichtung.Wenig, o.Mannschaften[0].Gesamt);
    }

    [Fact]
    public void ManuelleRundenmitteWirdGeschriebenUndGelesen()
    {
        Berechnungsoptionen o = Berechnungsoptionen.Standard with
        {
            Doppelrunde = true,
            AutomatischeMitte = false,
            Mitte1 = new DateOnly(2026, 11, 7),
            Mitte2 = new DateOnly(2027, 1, 16),
            Rundenplanung = Rundenplanung.NurRueckrunde,
        };
        byte[] inhalt = OptionenDatei.ErzeugenBytes(o);
        string text = Encoding.Latin1.GetString(inhalt);
        Assert.EndsWith(
            "<round-planing planing=\"rpSecondOnly\"/><double-round active=\"true\" automatic-mid-date=\"false\" mid-date-1=\"07.11.2026\" mid-date-2=\"16.01.2027\"/></andigenerator-options>\r\n",
            text,
            StringComparison.Ordinal);

        Berechnungsoptionen neu = LadenAus(inhalt);
        Assert.True(neu.Doppelrunde);
        Assert.False(neu.AutomatischeMitte);
        Assert.Equal(new DateOnly(2026, 11, 7), neu.Mitte1);
        Assert.Equal(new DateOnly(2027, 1, 16), neu.Mitte2);
        Assert.Equal(Rundenplanung.NurRueckrunde, neu.Rundenplanung);
    }

    [Fact]
    public void BeiAutomatikWerdenManuelleDatenNichtGeschrieben()
    {
        Berechnungsoptionen o = Berechnungsoptionen.Standard with { Mitte1 = new DateOnly(2026, 11, 7) };
        string text = Encoding.Latin1.GetString(OptionenDatei.ErzeugenBytes(o));
        Assert.DoesNotContain("mid-date-1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UngueltigesDatumSetztAllesZurueck()
    {
        Assert.Same(Berechnungsoptionen.Standard, AusText(Kopf +
            "<andigenerator-options><weighting gameday=\"coHoch\"/>" +
            "<double-round active=\"true\" automatic-mid-date=\"false\" mid-date-1=\"31.02.2026\"/></andigenerator-options>"));
    }

    [Fact]
    public void SpeichernUndLadenErgibtGleicheBytes()
    {
        string ordner = Path.Combine(Path.GetTempPath(), "andigen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            string pfad = Path.Combine(ordner, OptionenDatei.Dateiname);
            Berechnungsoptionen o = OptionenDatei.Laden(Datei("Verbandsliga Rheinland Süd-West 2026.options"));
            OptionenDatei.Speichern(pfad, o);
            Assert.Equal(File.ReadAllBytes(Datei("Verbandsliga Rheinland Süd-West 2026.options")), File.ReadAllBytes(pfad));
            Assert.False(File.Exists(pfad + ".tmp"));
        }
        finally
        {
            Directory.Delete(ordner, recursive: true);
        }
    }

    private static string Datei(string name) => Path.Combine(Testdaten.Datei(OptionenOrdner), name);

    private static Berechnungsoptionen AusText(string xml) =>
        LadenAus(Encoding.Latin1.GetBytes(xml));

    private static Berechnungsoptionen LadenAus(byte[] inhalt)
    {
        using var strom = new MemoryStream(inhalt);
        return OptionenDatei.Laden(strom);
    }
}
