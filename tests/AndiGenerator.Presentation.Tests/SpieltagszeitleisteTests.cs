// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class SpieltagszeitleisteTests
{
    private static readonly DateTime Sonntag = new(2026, 9, 6, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void JederSpieltagHatSeineTermineMitAnzahlUndUeberschneidungen()
    {
        // A spielt sein zweites Spiel eine Woche später: Spieltag 2 reicht in Spieltag 3 hinein.
        Diagrammdaten daten = Daten(
            ("A", [0, 14, 21]),
            ("B", [0, 7, 14]),
            ("C", [0, 7, 14]));

        IReadOnlyList<Spieltagszeile> zeilen = Spieltagszeitleiste.Ermitteln(daten);

        Assert.Equal([1, 2, 3], zeilen.Select(z => z.Nummer));
        Assert.Equal([(Tag(0), 3)], Kurz(zeilen[0]));
        Assert.Equal([(Tag(7), 2), (Tag(14), 1)], Kurz(zeilen[1]));
        Assert.Equal(["B", "C"], zeilen[1].Termine[0].Spiele.Select(s => s.Name));
        Assert.Equal(1, zeilen[1].Wochen);
        Assert.Equal([3], zeilen[1].Mit);
        Assert.Equal([new Spieltagsabschnitt(Tag(14), Tag(14))], zeilen[1].Ueberschneidungen);
        Assert.Empty(zeilen[0].Mit);
        Assert.False(zeilen[1].WieVorher);
    }

    [Fact]
    public void ZweiSpieleAmSelbenTagWerdenZweiZeilenAmSelbenTermin()
    {
        Diagrammdaten daten = Daten(
            ("A", [0, 0, 7, 7]),
            ("B", [0, 0, 14, 14]));

        IReadOnlyList<Spieltagszeile> zeilen = Spieltagszeitleiste.Ermitteln(daten);

        Assert.Equal([false, true, false, true], zeilen.Select(z => z.WieVorher));
        Assert.Equal(Kurz(zeilen[2]), Kurz(zeilen[3]));
        Assert.All(zeilen, z => Assert.Empty(z.Mit));
        Assert.Equal(1, Spieltagszeitleiste.Gleichzeitig(zeilen, DateOnly.FromDateTime(Sonntag.AddDays(-6))));
        Assert.Equal(1, Spieltagszeitleiste.Gleichzeitig(zeilen, DateOnly.FromDateTime(Sonntag.AddDays(8))));
        Assert.Equal(0, Spieltagszeitleiste.Gleichzeitig(zeilen, DateOnly.FromDateTime(Sonntag.AddDays(15))));
    }

    [Fact]
    public void SpieleEinerWocheStehenInEinemKreis()
    {
        // Samstag, Sonntag und der Mittwoch davor liegen in derselben Woche von Montag bis Sonntag.
        Diagrammdaten daten = Daten(
            ("A", [-1]),
            ("B", [0]),
            ("C", [-4]),
            ("D", [1]));

        Spieltagszeile zeile = Assert.Single(Spieltagszeitleiste.Ermitteln(daten));

        Assert.Equal([(Tag(0), 3), (Tag(1), 1)], Kurz(zeile));
        Assert.Equal(["C", "A", "B"], zeile.Termine[0].Spiele.Select(s => s.Name));
        Assert.Equal(1, zeile.Wochen);
    }

    [Fact]
    public void BereicheFassenFolgenZusammen()
    {
        Assert.Equal("1, 2, 5 – 8", Spieltagszeitleiste.Bereiche([1, 2, 5, 6, 7, 8]));
        Assert.Equal("3", Spieltagszeitleiste.Bereiche([3]));
        Assert.Equal(string.Empty, Spieltagszeitleiste.Bereiche([]));
    }

    [Fact]
    public void DiagrammZeigtSpieltageSpanneUndUeberschneidung()
    {
        Diagrammdaten daten = Daten(
            ("A", [0, 0, 28, 28]),
            ("B", [0, 0, 7, 7]),
            ("C", [7, 7, 14, 14]));
        var flaeche = new Messflaeche();

        Zeichengroesse groesse = new Diagrammzeichner(daten, flaeche).Zeichnen(Diagrammteil.Spieltage, 0);

        Assert.Contains("Spieltag 4", flaeche.Texte);
        Assert.Contains("am selben Termin", flaeche.Texte);
        Assert.Contains("3 Wo.", flaeche.Texte);
        Assert.Contains("überschneidet 1, 2", flaeche.Texte);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.Spieltage, 0));
    }

    [Fact]
    public void KreiseNennenDieMannschaftenBeimUeberfahren()
    {
        Diagrammdaten daten = Daten(
            ("A", [0]),
            ("B", [-1]),
            ("C", [7]));
        var zeichner = new Diagrammzeichner(daten, new Messflaeche());

        zeichner.Zeichnen(Diagrammteil.Spieltage, 0);

        Assert.Equal(2, zeichner.Hinweise.Count);
        Diagrammhinweis erster = zeichner.Hinweise[0];
        Assert.True(erster.Trifft(erster.X + erster.Radius, erster.Y));
        Assert.False(erster.Trifft(erster.X + erster.Radius + 5, erster.Y));
        string[] zeilen = erster.Text.Split(Environment.NewLine);
        Assert.Equal("Spieltag 1 · KW 36 (31.08. – 06.09.)", zeilen[0]);
        Assert.Equal(["Samstag, 05.09.", "   B (Heim)", "Sonntag, 06.09.", "   A (Heim)"], zeilen[1..]);
    }

    private static List<(DateOnly Woche, int Anzahl)> Kurz(Spieltagszeile zeile) => zeile.Termine.Select(t => (t.Woche, t.Mannschaften)).ToList();

    private static DateOnly Tag(int tage) => Spieltagszeitleiste.Montag(DateOnly.FromDateTime(Sonntag.AddDays(tage)));

    private static Diagrammdaten Daten(params (string Name, int[] Tage)[] mannschaften)
    {
        List<Diagrammmannschaft> liste = mannschaften
            .Select(m => new Diagrammmannschaft(m.Name, m.Tage.Select((t, i) => new Diagrammspiel(Sonntag.AddDays(t), i % 2 == 0, false, 1, 0)).ToList(), []))
            .ToList();
        DateTime letzter = Sonntag.AddDays(mannschaften.Max(m => m.Tage.Max()));
        return new Diagrammdaten(true, Sonntag, letzter, liste, true, [], false, mannschaften.Max(m => m.Tage.Length));
    }
}
