// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class WochenTests
{
    private static readonly DateOnly Montag = new(2026, 9, 7);
    private static readonly DateTime Samstag = new(2026, 9, 12, 18, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void RasterZeigtJeSpielEinenPunktUndDenAbstand()
    {
        // A spielt in Woche 1 zweimal und in Woche 3 dreimal, B in Woche 2 einmal.
        Diagrammmannschaft a = Mannschaft("A", [0, 0, 14, 14, 15]);
        Diagrammmannschaft b = Mannschaft("B", [7]);
        var woche = new Wochenstatistik("Hinrunde", [Montag, Montag.AddDays(7), Montag.AddDays(14)], [[2, 0, 3], [0, 1, 0]], [2, 1, 4]);
        var daten = new Diagrammdaten(true, Samstag, Samstag.AddDays(20), [a, b], false, [woche], false, 5);
        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);

        Zeichengroesse groesse = zeichner.Zeichnen(Diagrammteil.SpieleProWoche, 0);

        // Ein Punkt je Spiel (dazu der Punkt der Legende), keine Texte „2 / 2“ mehr; je Woche mit Spielen ein Hinweis. Rechts
        // „2 je Woche“ wie die Bewertung: A in Woche 1 einmal, in Woche 3 zweimal.
        Assert.Equal(7, flaeche.Punkte.Count(p => p.Art == "Kreis"));
        Assert.DoesNotContain(flaeche.Texte, t => t.Contains(" / ", StringComparison.Ordinal));
        Assert.Contains("Hinrunde", flaeche.Texte);
        Assert.Contains("4", flaeche.Texte);
        Assert.Contains("3", flaeche.Texte);
        Assert.Contains("zählt 2× als „2 Spiele die Woche“", zeichner.Hinweise[1].Text, StringComparison.Ordinal);
        Assert.Equal(3, zeichner.Hinweise.Count);
        Diagrammhinweis drei = zeichner.Hinweise[1];
        Assert.StartsWith("A: 3 Spiele in der Woche ab 21.09.2026", drei.Text, StringComparison.Ordinal);
        Assert.Contains("gegen X (Heim)", drei.Text, StringComparison.Ordinal);
        Assert.Contains("danach 5 Spiele in dieser Runde", drei.Text, StringComparison.Ordinal);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.SpieleProWoche, 0));
    }

    [Fact]
    public void KompaktspieltageNennenDieHalle()
    {
        List<Spiel> spiele =
        [
            new(Samstag, "A", "B", "Halle A"),
            new(Samstag.AddHours(2), "B", "C", string.Empty),
            new(Samstag.AddHours(4), "A", "C", "Halle A"),
        ];
        List<Diagrammmannschaft> mannschaften = new[] { "A", "B", "C" }
            .Select(n => new Diagrammmannschaft(n, spiele.Where(s => s.Heim == n || s.Gast == n).Select(s => new Diagrammspiel(s.Zeitpunkt, s.Heim == n, false, 1, 0, s.Heim == n ? s.Gast : s.Heim)).ToList(), []))
            .ToList();
        var woche = new Wochenstatistik("Hinrunde", [Montag], [[2], [2], [2]], [0]);
        var daten = new Diagrammdaten(true, Samstag, Samstag, mannschaften, false, [woche], false, 2, Kompaktplanung.Ausrichtertage(spiele));
        var zeichner = new Diagrammzeichner(daten, new Messflaeche());

        zeichner.Zeichnen(Diagrammteil.SpieleProWoche, 0);

        Assert.Contains("gegen C (in der Halle von A)", zeichner.Hinweise[1].Text, StringComparison.Ordinal);
        Assert.Contains("gegen B (eigene Halle)", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
    }

    private static Diagrammmannschaft Mannschaft(string name, int[] tage) =>
        new(name, tage.Select(t => new Diagrammspiel(Samstag.AddDays(t), true, false, 1, 0, "X")).ToList(), []);
}
