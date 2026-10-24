// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class HinRueckTests
{
    private static readonly DateTime Samstag = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(9, Abstandsstufe.ImSoll)]
    [InlineData(7, Abstandsstufe.ImSoll)]
    [InlineData(11, Abstandsstufe.ImSoll)]
    [InlineData(6, Abstandsstufe.Auffaellig)]
    [InlineData(5, Abstandsstufe.Auffaellig)]
    [InlineData(12, Abstandsstufe.Auffaellig)]
    [InlineData(13, Abstandsstufe.Auffaellig)]
    [InlineData(4, Abstandsstufe.WeitDaneben)]
    [InlineData(1, Abstandsstufe.WeitDaneben)]
    [InlineData(14, Abstandsstufe.WeitDaneben)]
    [InlineData(17, Abstandsstufe.WeitDaneben)]
    public void SollIstDerIdealeAbstandPlusMinusZweiSpiele(int abstand, Abstandsstufe stufe)
    {
        Assert.Equal(stufe, Diagrammzeichner.Bewerten(abstand, 9));
    }

    [Fact]
    public void GegnerStehenNurImHinweis()
    {
        // A: gegen Team B nach 2 Spielen (viel zu kurz), gegen C und D im Soll (9), gegen E viel zu lang (14), gegen F zu lang (12).
        Diagrammmannschaft a = Mannschaft("A", ("Team B", 2), ("Team C", 9), ("Team D", 9), ("Team E", 14), ("Team F", 12));
        var daten = new Diagrammdaten(true, Samstag, Samstag.AddDays(200), [a], true, [], false, 18);
        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);

        Zeichengroesse groesse = zeichner.Zeichnen(Diagrammteil.AbstandHeimAuswaerts, 0);

        // Je Abstand ein Punkt; C und D teilen sich einen Punkt mit der Anzahl.
        Assert.Equal(4, zeichner.Hinweise.Count);
        Assert.DoesNotContain(flaeche.Texte, t => t.Contains("Team", StringComparison.Ordinal));
        Diagrammhinweis soll = Assert.Single(zeichner.Hinweise, h => h.Text.Contains("Team C", StringComparison.Ordinal));
        Assert.Contains("Team D", soll.Text, StringComparison.Ordinal);
        Assert.StartsWith("Abstand 9 Spiele – im Soll (ideal 9)", soll.Text, StringComparison.Ordinal);
        Assert.StartsWith("Abstand 2 Spiele – viel zu kurz", zeichner.Hinweise[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("Abstand 12 Spiele – zu lang", zeichner.Hinweise[2].Text, StringComparison.Ordinal);
        Assert.StartsWith("Abstand 14 Spiele – viel zu lang", zeichner.Hinweise[3].Text, StringComparison.Ordinal);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.AbstandHeimAuswaerts, 0));
    }

    private static Diagrammmannschaft Mannschaft(string name, params (string Gegner, int Abstand)[] gegner) =>
        new(name, [], gegner.Select(g => (IReadOnlyList<Abstandslinie>)[new Abstandslinie(g.Gegner, Samstag, Samstag.AddDays(7 * g.Abstand), g.Abstand, 9)]).ToList());
}
