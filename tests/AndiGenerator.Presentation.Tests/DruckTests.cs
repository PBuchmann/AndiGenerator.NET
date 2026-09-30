// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Druckauswahl und Seitenumbruch des Ausdrucks (mit fester Zeichenbreite gemessen).</summary>
public sealed class DruckTests
{
    private static readonly Seitenformat Hoch = Seitenformat.A4Hoch;
    private static readonly Messflaeche Messung = new();

    [Fact]
    public void Druckauswahl_ist_gueltig_solange_etwas_Druckbares_gewaehlt_ist()
    {
        var ohne = new DruckauswahlViewModel([]);
        Assert.False(ohne.HatPlan);
        Assert.Null(ohne.Quelle);
        Assert.True(ohne.IstGueltig);
        var geaendert = new List<string?>();
        ohne.PropertyChanged += (_, e) => geaendert.Add(e.PropertyName);
        ohne.Terminwuensche = false;
        ohne.Nachbartermine = false;
        ohne.Nachbartermine = false;
        Assert.False(ohne.IstGueltig);
        Assert.Equal(["Terminwuensche", "IstGueltig", "Nachbartermine", "IstGueltig"], geaendert);

        var mit = new DruckauswahlViewModel([PlanQuelle.ClickTt]);
        Assert.True(mit.HatPlan);
        Assert.Same(PlanQuelle.ClickTt, mit.Quelle);
        Assert.False(mit.Querformat);
        mit.Terminwuensche = false;
        mit.Nachbartermine = false;
        mit.Kosten = false;
        mit.Spielplan = false;
        mit.Mannschaftsplaene = false;
        Assert.True(mit.IstGueltig);
        mit.MannschaftsplaeneMitNachbarn = false;
        Assert.True(mit.IstGueltig);
        mit.Diagramme = false;
        Assert.False(mit.IstGueltig);
        mit.Diagramme = true;
        mit.Quelle = null;
        Assert.False(mit.IstGueltig);
        mit.Querformat = true;
        Assert.True(mit.Querformat);
        Assert.False(mit.Kosten || mit.Spielplan || mit.Mannschaftsplaene || mit.Terminwuensche || mit.Nachbartermine || mit.MannschaftsplaeneMitNachbarn);
        Assert.True(mit.Diagramme);
    }

    [Fact]
    public void Text_wird_an_Wortgrenzen_umbrochen()
    {
        // Breite = Zeichen × 1 bei Größe 2.
        Assert.Equal(["aa bb", "cc"], Seitenumbruch.Umbrechen("aa bb cc", 5, 2, false, Messung));
        Assert.Equal(["abcdefgh"], Seitenumbruch.Umbrechen("abcdefgh", 3, 2, false, Messung));
        Assert.Equal(["a", "b", string.Empty], Seitenumbruch.Umbrechen("a\r\nb\n", 10, 2, false, Messung));
    }

    [Fact]
    public void Abschnitte_beginnen_auf_neuen_Seiten_und_lange_Texte_laufen_weiter()
    {
        // Größe 10 → Zeilenhöhe 13; zwischen Kopf (70) und Fuß (787,89) passen 55 Zeilen.
        List<Baustein> zeilen = Enumerable.Range(1, 60).Select(i => (Baustein)new Textzeile("Zeile " + i, 10, Farbe.Tinte)).ToList();
        var dokument = Dokument(new Abschnitt("A", zeilen), new Abschnitt("B", [new Abstand(20), new Textzeile("allein", 10, Farbe.Tinte, AbstandDavor: 30)]));

        IReadOnlyList<Druckseite> seiten = Seitenumbruch.Umbrechen(dokument, Hoch, Messung);

        Assert.Equal(["A", "A", "B"], seiten.Select(s => s.Abschnitt));
        Assert.Equal(55, seiten[0].Elemente.Count);
        Assert.Equal(5, seiten[1].Elemente.Count);
        TextElement allein = Assert.IsType<TextElement>(Assert.Single(seiten[2].Elemente));
        Assert.Equal(Hoch.InhaltOben, allein.Y);
    }

    [Fact]
    public void Lange_Zeilen_werden_auf_die_Nutzbreite_umbrochen()
    {
        string lang = string.Join(' ', Enumerable.Repeat("Wort", 100));
        var dokument = Dokument(new Abschnitt("T", [new Textzeile("Kopf", 10, Farbe.Tinte), new Textzeile(lang, 10, Farbe.Tinte, Einzug: 16, AbstandDavor: 10)]));

        Druckseite seite = Assert.Single(Seitenumbruch.Umbrechen(dokument, Hoch, Messung));

        List<TextElement> texte = seite.Elemente.OfType<TextElement>().ToList();
        Assert.True(texte.Count > 2);
        Assert.All(texte.Skip(1), t => Assert.True(t.X + Messung.TextBreite(t.Text, 10, false) <= Hoch.Rand + Hoch.Nutzbreite));
        Assert.Equal(Hoch.InhaltOben + 13 + 10, texte[1].Y);
    }

    [Fact]
    public void Tabellen_wiederholen_den_Kopf_und_halten_Ueberschriften_bei_ihrer_Zeile()
    {
        // Kopf 14, Zeile 13: nach 52 Zeilen passt die Überschrift (18,85) noch, mit ihrer ersten Zeile nicht mehr.
        var zeilen = new List<Tabellenzeile> { Tabellenzeile.Leer };
        zeilen.AddRange(Enumerable.Range(1, 52).Select(i => Zeile("z" + i, "x")));
        zeilen.Add(Tabellenzeile.Ueberschrift("Überschrift"));
        zeilen.Add(Zeile("danach", "y"));
        var dokument = Dokument(new Abschnitt("T", [new Tabelle(["K1", "K2"], zeilen, 10)]));

        IReadOnlyList<Druckseite> seiten = Seitenumbruch.Umbrechen(dokument, Hoch, Messung);

        Assert.Equal(2, seiten.Count);
        Assert.All(seiten, s => Assert.Equal("K1", Assert.IsType<TextElement>(s.Elemente[0]).Text));
        Assert.All(seiten, s => Assert.Equal(Hoch.InhaltOben, Assert.IsType<TextElement>(s.Elemente[0]).Y));
        Assert.DoesNotContain("Überschrift", Texte(seiten[0]));
        Assert.Equal(["K1", "K2", "Überschrift", "danach", "y"], Texte(seiten[1]));
    }

    [Fact]
    public void Zu_breite_Tabellen_werden_verkleinert_oder_brechen_die_letzte_Spalte_um()
    {
        string lang = string.Join(' ', Enumerable.Repeat("Wort", 60));
        var gefuellt = new Tabellenzelle("teuer", new Farbe(255, 128, 128));
        var verkleinert = new Tabelle(null, [new Tabellenzeile([new Tabellenzelle("links"), new Tabellenzelle(lang)], Farbe.Tinte)], 10);
        var umbrechend = new Tabelle(null, [new Tabellenzeile([gefuellt, new Tabellenzelle(lang)], Farbe.Akzent, Fett: true)], 10, LetzteSpalteUmbrechen: true);
        var dokument = Dokument(new Abschnitt("T", [verkleinert, umbrechend]));

        Druckseite seite = Assert.Single(Seitenumbruch.Umbrechen(dokument, Hoch, Messung));

        List<TextElement> texte = seite.Elemente.OfType<TextElement>().ToList();
        Assert.True(texte[0].Groesse < 10);
        Assert.Equal(texte[0].Groesse, texte[1].Groesse);
        Assert.All(texte.Skip(2), t => Assert.Equal(10, t.Groesse));
        Assert.True(texte.Count > 4);
        Assert.All(texte.Skip(2), t => Assert.True(t.Fett && t.Farbe == Farbe.Akzent));
        RechteckElement rechteck = Assert.Single(seite.Elemente.OfType<RechteckElement>());
        Assert.Equal(new Farbe(255, 128, 128), rechteck.Farbe);
        Assert.True(rechteck.Hoehe > 13);
    }

    [Fact]
    public void Matrix_hat_senkrechte_Spaltenkoepfe_zentrierte_Werte_und_Zeilenlinien()
    {
        var zeilen = new List<Tabellenzeile>
        {
            new([new Tabellenzelle("Mannschaft A"), new Tabellenzelle("1", Farbe.Tinte, Farbe.Weiss)], Farbe.Tinte),
            new([new Tabellenzelle("Summe"), new Tabellenzelle("12")], Farbe.Tinte, Fett: true),
        };
        var dokument = Dokument(new Abschnitt("K", [new Tabelle(["Mannschaft", "eine lange Kostenart"], zeilen, 10, Matrix: true)]));

        Druckseite seite = Assert.Single(Seitenumbruch.Umbrechen(dokument, Hoch, Messung));

        SenkrechtElement kopf = Assert.Single(seite.Elemente.OfType<SenkrechtElement>());
        Assert.Equal("eine lange Kostenart", kopf.Text);
        TextElement mannschaft = seite.Elemente.OfType<TextElement>().First();
        Assert.Equal("Mannschaft", mannschaft.Text);

        // Der Kopf ist so hoch wie der längste senkrechte Text (20 Zeichen × 5) + 6 + Linie.
        TextElement a = seite.Elemente.OfType<TextElement>().Single(t => t.Text == "Mannschaft A");
        Assert.Equal(Hoch.InhaltOben + 107, a.Y, 6);
        Assert.Equal(kopf.Y + 3, a.Y - 1, 6);

        // Werte stehen in der Mitte ihrer Spalte (Breite 16 = 1,6 × Größe) und behalten ihre Schriftfarbe.
        TextElement eins = seite.Elemente.OfType<TextElement>().Single(t => t.Text == "1");
        Assert.Equal(Farbe.Weiss, eins.Farbe);
        double spalte = eins.X - ((16 - Messung.TextBreite("1", 10, false)) / 2);
        TextElement zwoelf = seite.Elemente.OfType<TextElement>().Single(t => t.Text == "12");
        Assert.Equal(spalte + ((16 - Messung.TextBreite("12", 10, true)) / 2), zwoelf.X, 6);

        // Kopflinie, je Zeile eine feine Linie und eine Linie über der Summe.
        Assert.Equal(4, seite.Elemente.OfType<LinienElement>().Count());
        Assert.Single(seite.Elemente.OfType<RechteckElement>());
    }

    [Fact]
    public void Seiten_zeichnen_Titel_Inhalt_und_Fusszeile()
    {
        var dokument = Dokument(new Abschnitt("Spielplan", [new Tabelle(["Kopf"], [Zeile("a", "b")], 10)]));
        IReadOnlyList<Druckseite> seiten = Seitenumbruch.Umbrechen(dokument, Seitenformat.A4Quer, Messung);
        var flaeche = new Messflaeche();

        seiten[0].Zeichnen(flaeche, dokument, Seitenformat.A4Quer, 1, seiten.Count);

        Assert.Equal(["Spielplan", "Kopf", "a", "b", "Staffel · Plan · 29.09.2026 10:05", "Seite 1 von 1"], flaeche.Texte);

        // Titel: feine Linie und Akzentstrich; dazu Tabellenkopf und Fußzeile.
        Assert.Equal(4, flaeche.Linien);
        Assert.Equal(0, flaeche.Rechtecke);
        Assert.True(Seitenformat.A4Quer.Breite > Seitenformat.A4Quer.Hoehe);
    }

    [Fact]
    public void Grafiken_werden_verkleinert_und_versetzt_gezeichnet()
    {
        static void Zeichnung(IZeichenflaeche f)
        {
            f.Text("t", 100, 50, 10, Farbe.Tinte, false);
            f.TextSenkrecht("s", 10, 20, 10, Farbe.Tinte);
            f.Kreis(0, 0, 5, Farbe.Schlecht, Farbe.Tinte, 1);
            f.Linie(0, 0, 10, 10, Farbe.Tinte, 1);
            f.Rechteck(0, 0, 10, 10, Farbe.Grau);
            Assert.Equal(5, f.TextBreite("ab", 5, false), 6);
        }

        var grafiken = new List<Baustein>
        {
            new Grafik(0, 10, Zeichnung),
            new Grafik(Hoch.Nutzbreite * 2, 100, Zeichnung),
            new Grafik(100, 2000, Zeichnung, AbstandDavor: 5),
            new Grafik(100, 100, Zeichnung, AbstandDavor: 5),
        };
        var dokument = Dokument(new Abschnitt("G", grafiken));

        IReadOnlyList<Druckseite> seiten = Seitenumbruch.Umbrechen(dokument, Hoch, Messung);

        // Die hohe Grafik passt nur verkleinert und allein auf eine Seite; die letzte beginnt deshalb eine dritte.
        Assert.Equal(3, seiten.Count);
        GrafikElement breit = Assert.IsType<GrafikElement>(Assert.Single(seiten[0].Elemente));
        Assert.Equal(0.5, breit.Skala, 6);
        Assert.Equal(Hoch.InhaltOben, breit.Y);
        GrafikElement hoch = Assert.IsType<GrafikElement>(Assert.Single(seiten[1].Elemente));
        Assert.Equal(Hoch.InhaltOben, hoch.Y);
        Assert.Equal((Hoch.InhaltUnten - Hoch.InhaltOben) / 2000, hoch.Skala, 6);
        GrafikElement klein = Assert.IsType<GrafikElement>(Assert.Single(seiten[2].Elemente));
        Assert.Equal(1, klein.Skala);
        Assert.Equal(Hoch.InhaltOben, klein.Y);

        var flaeche = new Messflaeche();
        breit.Zeichnen(flaeche);
        Assert.Equal(("Text", Hoch.Rand + 50, Hoch.InhaltOben + 25, 5.0), flaeche.Punkte[0]);
        Assert.Equal(("Senkrecht", Hoch.Rand + 5, Hoch.InhaltOben + 10, 5.0), flaeche.Punkte[1]);
        Assert.Equal(("Kreis", Hoch.Rand, Hoch.InhaltOben, 2.5), flaeche.Punkte[2]);
        Assert.Equal(1, flaeche.Linien);
        Assert.Equal(1, flaeche.Rechtecke);
    }

    [Fact]
    public void Unbekannte_Bausteine_werden_abgewiesen()
    {
        var dokument = Dokument(new Abschnitt("T", [new Fremd(1)]));
        Assert.Throws<ArgumentException>(() => Seitenumbruch.Umbrechen(dokument, Hoch, Messung));
    }

    private static Druckdokument Dokument(params Abschnitt[] abschnitte) =>
        new("Staffel", "Plan", new DateTime(2026, 9, 29, 10, 5, 0, DateTimeKind.Local), abschnitte);

    private static Tabellenzeile Zeile(string links, string rechts) => new([new Tabellenzelle(links), new Tabellenzelle(rechts)], Farbe.Tinte);

    private static List<string> Texte(Druckseite seite) => seite.Elemente.OfType<TextElement>().Select(t => t.Text).ToList();

    private sealed record Fremd(int Wert) : Baustein;
}
