// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class TerminwunschzeichnerTests
{
    private static readonly DateOnly Montag = new(2026, 9, 7);

    [Fact]
    public void KalenderZeigtJedenTagAnSeinemPlatz()
    {
        // A: Samstag Heimspielwunsch, Dienstag frei, in der zweiten Woche am Samstag Wunsch und Sperre zugleich. B: nichts.
        var a = new MannschaftsTerminwuensche(
            "A",
            [
                new(Montag.AddDays(5), Terminwunschart.Wunsch),
                new(Montag.AddDays(1), Terminwunschart.Sperrtermin),
                new(Montag.AddDays(12), Terminwunschart.Wunsch),
                new(Montag.AddDays(12), Terminwunschart.Sperrtermin),
            ],
            []);
        var b = new MannschaftsTerminwuensche("B", [], []);
        var u = new Terminwunschuebersicht(Montag, Montag.AddDays(13), false, [], [a, b]);
        var flaeche = new Messflaeche();
        var zeichner = new Terminwunschzeichner(u, flaeche, mitLegende: true);

        Zeichengroesse groesse = zeichner.Zeichnen();

        // Je Mannschaft und Woche mit Einträgen ein Hinweis; B hat keinen.
        Assert.Equal(2, zeichner.Hinweise.Count);
        Assert.StartsWith("A, KW 37 (07.09. – 13.09.2026)", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
        Assert.Contains("Di 08.09.  frei (Sperrtermin)", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
        Assert.Contains("Sa 12.09.  Heimspielwunsch", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
        Assert.Contains("Sa 19.09.  frei (Sperrtermin)", zeichner.Hinweise[1].Text, StringComparison.Ordinal);

        // 2 Mannschaften × 2 Wochen × 7 Tage, der Tag mit Wunsch und Sperre geteilt: 29 Felder, dazu 3 der Legende (ohne Koppeltermin) und das Band.
        Assert.Equal(29 + 3 + 1, flaeche.Rechtecke);
        Assert.Contains("frei (Sperrtermin)", flaeche.Texte);
        Assert.DoesNotContain("Koppeltermin", flaeche.Texte);
        Assert.Contains("37", flaeche.Texte);
        Assert.Contains("38", flaeche.Texte);
        Assert.Equal(groesse, new Terminwunschzeichner(u, null, mitLegende: true).Zeichnen());
    }
}
