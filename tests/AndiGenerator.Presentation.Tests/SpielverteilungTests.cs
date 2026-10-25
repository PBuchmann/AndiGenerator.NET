// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class SpielverteilungTests
{
    private static readonly DateTime Samstag = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void SpieleEinesTagesGehoerenZusammen()
    {
        Diagrammmannschaft m = Mannschaft("A", [0, 0, 7, 10]);

        IReadOnlyList<(DateOnly Tag, IReadOnlyList<Diagrammspiel> Spiele)> tage = Diagrammzeichner.Spieltermine(m);

        Assert.Equal([2, 1, 1], tage.Select(t => t.Spiele.Count));
    }

    [Fact]
    public void DiagrammZeigtDoppelspieltageKurzeAbstaendeUndPausen()
    {
        // A: Doppelspieltag, dann 3 Tage, dann eine Pause von 4 Wochen. B: drei Spiele an aufeinanderfolgenden Tagen.
        Diagrammdaten daten = Daten(Mannschaft("A", [0, 0, 7, 10, 38]), Mannschaft("B", [0, 1, 2, 3]));
        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);

        Zeichengroesse groesse = zeichner.Zeichnen(Diagrammteil.Spielverteilung, 0);

        Assert.Contains("3 T.", flaeche.Texte);
        Assert.Contains("28 T.", flaeche.Texte);

        // Drei Abstände von einem Tag passen nicht nebeneinander: einer oben, einer unten, der dritte nur im Hinweis – nichts
        // überschreibt sich.
        Assert.Equal(2, flaeche.Texte.Count(t => t == "1 T."));

        // Rechts die Zählung der Bewertung: A zwei Spiele kurz nacheinander (Doppelspieltag, 3 Tage), eins in derselben Woche,
        // eine Pause; B drei kurz nacheinander, zwei in derselben Woche (Sa/So und Mo/Di), keine Pause.
        Assert.Equal(["2", "1", "1"], RechteSpalten(flaeche, "A"));
        Assert.Equal(["3", "2", "0"], RechteSpalten(flaeche, "B"));

        // Je Spiel ein Hinweis mit Tag, Gegner und Abstand.
        Assert.Equal(9, zeichner.Hinweise.Count);
        Assert.Contains(zeichner.Hinweise, h => h.Text.Contains("Doppelspieltag: 2. Spiel des Tages", StringComparison.Ordinal));
        Assert.Contains(zeichner.Hinweise, h => h.Text.Contains("3 Tage nach dem vorigen Spiel", StringComparison.Ordinal));
        Assert.StartsWith("Samstag, 05.09.2026, 18:00 Uhr" + Environment.NewLine + "Heimspiel gegen X", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.Spielverteilung, 0));
    }

    [Fact]
    public void KompaktspieltageZeigenDenSpielortUndHebenDenSpieltagHervor()
    {
        // Dreier in der Halle von A: B hat gegen C Heimrecht laut Plan, spielt aber in fremder Halle.
        List<Spiel> spiele =
        [
            new(Samstag, "A", "B", "Halle A"),
            new(Samstag.AddHours(2), "B", "C", string.Empty),
            new(Samstag.AddHours(4), "A", "C", "Halle A"),
        ];
        List<Diagrammmannschaft> mannschaften = new[] { "A", "B", "C" }
            .Select(n => new Diagrammmannschaft(n, spiele.Where(x => x.Heim == n || x.Gast == n).Select(x => new Diagrammspiel(x.Zeitpunkt, x.Heim == n, false, 1, 0, x.Heim == n ? x.Gast : x.Heim)).ToList(), []))
            .ToList();
        var daten = new Diagrammdaten(true, Samstag, Samstag, mannschaften, false, [], false, 2, Kompaktplanung.Ausrichtertage(spiele));
        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);

        zeichner.Zeichnen(Diagrammteil.Spielverteilung, 0);

        Diagrammhinweis b = zeichner.Hinweise.Single(h => h.Text.Contains("gegen C", StringComparison.Ordinal) && h.Text.Contains("Halle von A", StringComparison.Ordinal));
        Assert.DoesNotContain("Heimspiel", b.Text, StringComparison.Ordinal);
        Assert.Equal(3, b.Marken!.Count);
        Assert.Contains("in der eigenen Halle (Ausrichter)", flaeche.Texte);
        Assert.All(zeichner.Hinweise, h => Assert.Equal(3, h.Marken!.Count));
    }

    /// <summary>Die drei Zahlen rechts in der Zeile einer Mannschaft (die letzten drei Texte vor der nächsten Zeile).</summary>
    private static string[] RechteSpalten(Messflaeche flaeche, string name)
    {
        int start = flaeche.Texte.IndexOf(name);
        int ende = flaeche.Texte.FindIndex(start + 1, t => t is "A" or "B" or "Heimspiel");
        return flaeche.Texte.GetRange(ende - 3, 3).ToArray();
    }

    private static Diagrammmannschaft Mannschaft(string name, int[] tage) =>
        new(name, tage.Select((t, i) => new Diagrammspiel(Samstag.AddDays(t), i % 2 == 0, false, 1, 0, "X")).ToList(), []);

    private static Diagrammdaten Daten(params Diagrammmannschaft[] mannschaften) =>
        new(true, Samstag, Samstag.AddDays(40), mannschaften, true, [], false, mannschaften.Max(m => m.Spiele.Count));
}
