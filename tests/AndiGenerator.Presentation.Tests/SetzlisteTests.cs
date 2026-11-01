// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class SetzlisteTests
{
    private static readonly DateTime Samstag = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(0, 0x4F)]
    [InlineData(-5, 0x4F)]
    [InlineData(1, 0xD9)]
    [InlineData(2, 0xD9)]
    [InlineData(3, 0xB4)]
    public void FarbeFolgtDenVerstoessen(int ueberschuss, byte rot)
    {
        // Nur zu weit entfernte Gegner sind Verstöße (orange, ab 3 Plätzen rot); zu nahe sind kein Verstoß und grün.
        Assert.Equal(rot, Diagrammzeichner.Setzlistenfarbe(ueberschuss).R);
    }

    [Fact]
    public void ZaehltWieDieBewertung()
    {
        // Vier Mannschaften, eine Runde: Gewünscht sind 3, 2, 1 Plätze Abstand. A spielt 1, 3, 2: das 2. Spiel (3 statt 2) und
        // das 3. (2 statt 1) zählen als Verstoß, das 1. (1 statt 3) nicht – es ist nur ungünstig.
        Diagrammmannschaft a = Mannschaft("A", [1, 3, 2]);
        var andere = new[] { "B", "C", "D" }.Select(n => Mannschaft(n, [3, 2, 1])).ToList();
        var daten = new Diagrammdaten(true, Samstag, Samstag.AddDays(20), [a, .. andere], false, [], true, 3);
        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);

        Zeichengroesse groesse = zeichner.Zeichnen(Diagrammteil.Setzliste, 0);

        Assert.Equal("2", flaeche.Texte[flaeche.Texte.IndexOf("A") + 1]);
        Assert.Equal(12, zeichner.Hinweise.Count);
        Assert.Contains("zählt als Verstoß", zeichner.Hinweise[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("zählt", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
        Assert.Contains("1 Plätze entfernt, gewünscht 3", zeichner.Hinweise[0].Text, StringComparison.Ordinal);

        // Mit Bewertung steht rechts deren Zahl.
        var verstoesse = new Diagrammverstoesse(
            new Dictionary<string, Mannschaftsverstoesse> { ["A"] = new(null, null, null, null, null, 5, []) },
            new HashSet<int>(),
            new HashSet<int>());
        var mitBewertung = new Messflaeche();
        new Diagrammzeichner(daten with { Verstoesse = verstoesse }, mitBewertung).Zeichnen(Diagrammteil.Setzliste, 0);
        Assert.Equal("5", mitBewertung.Texte[mitBewertung.Texte.IndexOf("A") + 1]);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.Setzliste, 0));
    }

    [Fact]
    public void JedeHalbrundeHatIhreTreppe()
    {
        // Runden sind Indizes ab 0 (wie aus der Bewertung): Hin- und Rückrunde stehen nebeneinander, nicht übereinander.
        List<Diagrammmannschaft> mannschaften = new[] { "A", "B", "C", "D" }
            .Select(n => new Diagrammmannschaft(n, Enumerable.Range(0, 6).Select(i => new Diagrammspiel(Samstag.AddDays(7 * i), true, false, i < 3 ? 0 : 1, 3 - (i % 3), "X")).ToList(), []))
            .ToList();
        var daten = new Diagrammdaten(true, Samstag, Samstag.AddDays(50), mannschaften, true, [], true, 6);
        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);

        zeichner.Zeichnen(Diagrammteil.Setzliste, 0);

        Assert.Contains("Hinrunde", flaeche.Texte);
        Assert.Contains("Rückrunde", flaeche.Texte);
        List<double> xa = zeichner.Hinweise.Take(6).Select(h => h.X).ToList();
        Assert.Equal(6, xa.Distinct().Count());
        Assert.True(xa[3] > xa[2]);

        // In beiden Runden beginnt die Treppe von vorn: kein Spiel ist ein Verstoß.
        Assert.Equal("0", flaeche.Texte[flaeche.Texte.IndexOf("A") + 1]);
    }

    [Fact]
    public void DoppelrundeNenntDieViertelrunden()
    {
        // Vier Viertelrunden: vier Treppen nebeneinander mit den Namen der Runden.
        List<Diagrammmannschaft> mannschaften = new[] { "A", "B", "C", "D" }
            .Select(n => new Diagrammmannschaft(n, Enumerable.Range(0, 12).Select(i => new Diagrammspiel(Samstag.AddDays(7 * i), true, false, i / 3, 3 - (i % 3), "X")).ToList(), []))
            .ToList();
        IReadOnlyList<string> namen = Rundennamen.Fuer(Domain.Optionen.Berechnungsoptionen.Standard with { Doppelrunde = true });
        var daten = new Diagrammdaten(true, Samstag, Samstag.AddDays(90), mannschaften, true, [], true, 12, Rundennamen: namen);
        var flaeche = new Messflaeche();

        new Diagrammzeichner(daten, flaeche).Zeichnen(Diagrammteil.Setzliste, 0);

        Assert.Equal(["1. Vorrunde", "2. Vorrunde", "1. Rückrunde", "2. Rückrunde"], namen);
        Assert.All(namen, n => Assert.Contains(n, flaeche.Texte));
        Assert.DoesNotContain("Hinrunde", flaeche.Texte);
        Assert.Equal("0", flaeche.Texte[flaeche.Texte.IndexOf("A") + 1]);
    }

    private static Diagrammmannschaft Mannschaft(string name, int[] abstaende) =>
        new(name, abstaende.Select((ab, i) => new Diagrammspiel(Samstag.AddDays(7 * i), i % 2 == 0, false, 1, ab, "X")).ToList(), []);
}
