// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Übersicht der Kompaktspieltage für die Vereine im Ausdruck.</summary>
public sealed class KompaktdruckTests
{
    private static readonly DateOnly Sonntag = new(2026, 9, 13);

    [Fact]
    public void Je_Teil_eine_Seite_und_je_Mannschaft_genau_eine()
    {
        IReadOnlyList<Abschnitt> abschnitte = Abschnitte(Auswertung(), (_, _) => string.Empty);

        Assert.Equal(["Meldungen und Plan", "Alle Fahrten", "Kompaktspieltage", "TTC A", "TTC B", "TTC C", "TTC D"], abschnitte.Select(x => x.Titel));
        var raster = new Messflaeche();
        Assert.IsType<Grafik>(Assert.Single(abschnitte[0].Bausteine)).Zeichnung(raster);
        Assert.Contains("Jugend 19 Testliga (HR)", raster.Texte);
        Assert.Equal(2, abschnitte[1].Bausteine.OfType<Grafik>().Count());
        var f = new Messflaeche();
        abschnitte[1].Bausteine.OfType<Grafik>().Last().Zeichnung(f);
        Assert.Contains("km je Mannschaft", f.Texte);
        Assert.Contains("1× Ausrichter", f.Texte);
        Assert.Contains("30", f.Texte);
    }

    [Theory]
    [InlineData("TTC Wirges", "TTC Wirges", null)]
    [InlineData("TTG Mündersbach/Höchstenbach", "TTG Mündersbach/", "Höchstenbach")]
    [InlineData("TTC Zugbrücke Grenzau", "TTC Zugbrücke", "Grenzau")]
    [InlineData("RSV Klein-Winternheim", "RSV Klein-", "Winternheim")]
    [InlineData("Wandsbeker Turnerbund 1861", "Wandsbeker", "Turnerbund 1861")]
    [InlineData("Donaudampfschifffahrt", "Donaudampfschifffahrt", null)]
    public void Lange_Namen_im_Rasterkopf_in_zwei_Zeilen(string name, string erste, string? zweite)
    {
        IReadOnlyList<string> zeilen = Kompaktdruck.Kopfzeilen(name);

        Assert.Equal(zweite is null ? [erste] : [erste, zweite], zeilen);
    }

    [Fact]
    public void Raster_steht_zentriert_und_der_Kopf_richtet_sich_nach_der_laengsten_Zeile()
    {
        Kompaktauswertung a = Auswertung();

        Assert.True(Kompaktdruck.Raster(a, (null, null)).Zentriert);
        Assert.Equal(104, Kompaktdruck.Kopfhoehe(["TTC A"]));
        Assert.Equal(Math.Ceiling(12 + ("TTG Mündersbach/".Length * 6.8)), Kompaktdruck.Kopfhoehe(["TTG Mündersbach/Höchstenbach"]));

        var dokument = new Druckdokument("Klasse", "Plan", DateTime.Now, [new Abschnitt("Raster", [Kompaktdruck.Raster(a, (null, null))])]);
        GrafikElement element = Assert.Single(Assert.Single(Seitenumbruch.Umbrechen(dokument, Seitenformat.A4Hoch, new Messflaeche())).Elemente.OfType<GrafikElement>());
        Assert.True(element.X > Seitenformat.A4Hoch.Rand + 10);
    }

    [Fact]
    public void Bei_vielen_Tagen_stehen_Klassenname_und_Raster_auf_einer_Seite()
    {
        // Höher als eine Seite: bisher rutschte das Raster auf Seite 2, der Klassenname blieb allein auf Seite 1.
        Kompaktauswertung a = Auswertung();
        List<DateOnly> tage = Enumerable.Range(0, 30).Select(i => Sonntag.AddDays(i * 3)).ToList();
        var viele = new Kompaktauswertung(tage, a.Mannschaften, a.Spieltage, [], 0, 4);
        IReadOnlyList<Abschnitt> abschnitte = Abschnitte(viele, (_, _) => string.Empty);
        Grafik raster = Assert.IsType<Grafik>(Assert.Single(abschnitte[0].Bausteine));
        Assert.True(raster.Hoehe > Seitenformat.A4Hoch.Hoehe);

        var dokument = new Druckdokument("Klasse", "Plan", DateTime.Now, [abschnitte[0]]);
        IReadOnlyList<Druckseite> seiten = Seitenumbruch.Umbrechen(dokument, Seitenformat.A4Hoch, new Messflaeche());

        Assert.Single(seiten);
    }

    [Fact]
    public void Spieltag_mit_Halle_und_Begegnungen_ohne_Hinweise_fuer_den_Klassenleiter()
    {
        IReadOnlyList<Abschnitt> abschnitte = Abschnitte(Auswertung(), (m, lokal) => m == "TTC A" && lokal == "2" ? "Halle 2 in A-Dorf" : string.Empty);

        List<string> texte = Texte(abschnitte[2]);
        string datum = Sonntag.ToString("ddd dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        Assert.Contains(datum + " · 4er-Spieltag · Ausrichter: TTC A", texte);
        Assert.Contains("Halle: Halle 2 in A-Dorf", texte);
        Assert.Contains("TTC B – TTC D", texte);
        Assert.DoesNotContain(texte, x => x.Contains("verlegen", StringComparison.Ordinal));
    }

    [Fact]
    public void Je_Mannschaft_Karte_Ort_und_Gegner_mit_Uhrzeit()
    {
        IReadOnlyList<Abschnitt> abschnitte = Abschnitte(Auswertung(), (_, _) => string.Empty);

        string datum = Sonntag.ToString("ddd dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        List<string> a = Texte(abschnitte[3]);
        Assert.Contains("1 Heimspieltag · 30 km Luftlinie (hin und zurück)", a);
        Assert.Contains(datum + " · 4er-Spieltag · Heim (Ausrichter)", a);
        Assert.Contains("Halle: Standardhalle A", a);
        Assert.Contains("gegen TTC C", a);
        Assert.Contains("gegen TTC D", a);
        Assert.Empty(abschnitte[3].Bausteine.OfType<Grafik>());

        // Je Spieltag: Überschrift, Halle, dann je Spiel eine Zeile mit Beginn und Gegner.
        Tabelle tabelle = Assert.Single(abschnitte[5].Bausteine.OfType<Tabelle>());
        Assert.Equal(["Beginn", "Spiel"], tabelle.Kopf);
        Assert.Equal(datum + " · 4er-Spieltag · bei TTC A", tabelle.Zeilen[0].Zellen[0].Text);
        Assert.Equal(Zeilenart.Ueberschrift, tabelle.Zeilen[0].Art);
        Assert.Equal("Halle: Standardhalle A", tabelle.Zeilen[1].Zellen[1].Text);
        Assert.Equal(["11:00", "gegen TTC A"], tabelle.Zeilen[2].Zellen.Select(z => z.Text));
        Assert.Equal(["14:00", "gegen TTC B"], tabelle.Zeilen[3].Zellen.Select(z => z.Text));
        Assert.Single(abschnitte[5].Bausteine.OfType<Grafik>());
    }

    [Fact]
    public void Ohne_Spieltage_ein_Hinweis()
    {
        var leer = new Kompaktauswertung([], [], [], [], 0, 0);

        IReadOnlyList<Abschnitt> abschnitte = Abschnitte(leer, (_, _) => string.Empty);

        Assert.IsType<Textzeile>(Assert.Single(Assert.Single(abschnitte).Bausteine));
    }

    [Fact]
    public void Raster_mit_Ringen_und_Legende()
    {
        Kompaktauswertung a = Auswertung();
        var f = new Messflaeche();

        Grafik raster = Kompaktdruck.Raster(a, (a.Spieltage[0], null));
        raster.Zeichnung(f);

        Assert.True(raster.Breite >= 470 && raster.Hoehe > 104);
        Assert.Contains("TTC B", f.Texte);
        Assert.Contains("Spitzenspieltag", f.Texte);
        Assert.Contains(f.Punkte, p => p.Art == "Kreis" && Math.Abs(p.Groesse - 8.25) < 1e-9);
        Assert.True(f.Vielecke > 4);
    }

    [Fact]
    public void Karten_zeichnen_Hallen_und_Fahrten()
    {
        Kompaktauswertung a = Auswertung();
        Kartenbild bild = Kompaktfahrtenkarte.Mannschaft(a, "TTC C");
        var f = new Messflaeche();

        Kompaktdruck.Karte(bild, 330, 0).Zeichnung(f);

        Assert.InRange(f.Rechtecke, 1, 1 + bild.Staedte.Count);
        Assert.Contains("TTC C", f.Texte);
        Assert.Contains(f.Punkte, p => p.Art == "Kreis");
    }

    private static IReadOnlyList<Abschnitt> Abschnitte(Kompaktauswertung a, Func<string, string, string> halle) =>
        Kompaktdruck.Abschnitte(a, halle, "Jugend 19 Testliga (HR)", (null, null));

    private static Kompaktauswertung Auswertung()
    {
        DateTime elf = Sonntag.ToDateTime(new TimeOnly(11, 0));
        DateTime vierzehn = Sonntag.ToDateTime(new TimeOnly(14, 0));
        Spiel ac = new(elf, "TTC A", "TTC C", "2");
        Spiel bd = new(elf, "TTC B", "TTC D", string.Empty);
        Spiel ad = new(vierzehn, "TTC A", "TTC D", "2");
        Spiel cb = new(vierzehn, "TTC C", "TTC B", string.Empty);
        var tag = new Kompaktspieltag(Sonntag, Turnierform.Vierer, "TTC A", ["TTC A", "TTC B", "TTC C", "TTC D"], [ac, bd, ad, cb], [bd], [], [], 30);
        List<Kompaktmannschaft> mannschaften = new[] { "TTC A", "TTC B", "TTC C", "TTC D" }
            .Select((n, i) => new Kompaktmannschaft(n, new Koordinate(50 + (i * 0.1), 7 + (i * 0.1)), "Standardhalle " + n[^1], null, string.Empty, 30, n == "TTC A" ? 1 : 0))
            .ToList();
        return new Kompaktauswertung([Sonntag], mannschaften, [tag], [], 0, 4);
    }

    private static List<string> Texte(Abschnitt abschnitt) =>
        [
            .. abschnitt.Bausteine.OfType<Textzeile>().Select(z => z.Text),
            .. abschnitt.Bausteine.OfType<Tabelle>().SelectMany(t => t.Zeilen).SelectMany(z => z.Zellen).Select(z => z.Text),
        ];
}
