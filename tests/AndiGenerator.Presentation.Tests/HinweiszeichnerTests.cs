// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class HinweiszeichnerTests
{
    [Fact]
    public void KastenIstSoBreitWieDieFetteErsteZeile()
    {
        // Die Stelle liegt am rechten Rand: Der Kasten steht links davon und endet 14 Punkte vor ihr – so ist seine Breite messbar.
        const string Kopf = "Eine lange fette erste Zeile";
        var h = new Diagrammhinweis(1000, 100, 5, Kopf + Environment.NewLine + "kurz");
        var flaeche = new Messflaeche();

        (double x, _) = Hinweiszeichner.Zeichnen(flaeche, h, 1010, 500);

        double breite = h.Links - 14 - x;
        Assert.True(breite >= flaeche.TextBreite(Kopf, 12, true) + 16, $"Kasten {breite} zu schmal");
    }
}
