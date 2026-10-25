// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Schranke;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Kachel „Berechnete Pläne“ im Automodus: woran HiGHS gerade rechnet.</summary>
public sealed class HighsanzeigeTests
{
    private static readonly Laufzeitregel Grenze = new(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(15));

    [Fact]
    public void Untergrenze_zeigt_Schritt_Zeit_und_was_danach_kommt()
    {
        var summeB = new Schrankenfortschritt("Summe B", 2, 4, null);

        Highsanzeige a = Highsanzeige.Bilden(true, false, summeB, TimeSpan.FromSeconds(12), Grenze, false, null);

        Assert.Equal("HiGHS · Untergrenze", a.Titel);
        Assert.Equal("Summe B", a.Schritt);
        Assert.Equal("12 s · noch mindestens 8 s", a.Zeit);
        Assert.Equal(30, a.Prozent, 3);
        Assert.Equal("dann A+B", a.Danach);
        Assert.True(a.Rechnet);
        Assert.Equal([true, true, false, false, false, false], a.Schritte.Select(s => s.Fertig));
        Assert.Equal("Stufe B: läuft", a.Schritte[2].Hinweis);

        // Nach der Mindestzeit weiter, solange es vorangeht; über der Höchstzeit (mehrere Rechnungen in einem Schritt) bleibt der
        // Balken voll.
        Highsanzeige weiter = Highsanzeige.Bilden(true, false, summeB, TimeSpan.FromSeconds(100), Grenze, false, null);
        Assert.Equal("100 s · weiter, solange es vorangeht (höchstens 3 min)", weiter.Zeit);
        Assert.Equal(75, weiter.Prozent, 3);
        Highsanzeige lang = Highsanzeige.Bilden(true, false, summeB, TimeSpan.FromSeconds(200), Grenze, false, null);
        Assert.Equal("200 s · mehrere Rechnungen nacheinander", lang.Zeit);
        Assert.Equal(100, lang.Prozent, 3);

        // A+B und Stufe C erkennt die Anzeige am Text (beide melden den letzten Schritt).
        Assert.Equal("dann Stufe C", Highsanzeige.Bilden(true, false, new Schrankenfortschritt("A und B zusammen", 3, 4, null), TimeSpan.Zero, Grenze, false, null).Danach);
        Assert.Equal("dann Teilneuplanung", Highsanzeige.Bilden(true, false, new Schrankenfortschritt("Stufe C und Feinschliff", 3, 4, null), TimeSpan.Zero, Grenze, false, null).Danach);

        // Angehalten: kein Balken, HiGHS rechnet nicht.
        Highsanzeige pause = Highsanzeige.Bilden(true, true, summeB, TimeSpan.FromSeconds(12), Grenze, false, null);
        Assert.False(pause.Rechnet);
        Assert.False(pause.ZeigtAnteil);
        Assert.Equal("angehalten", pause.Zeit);
    }

    [Fact]
    public void Nach_der_Untergrenze_die_Teilneuplanung()
    {
        var stand = new Teilneuplanungsstand("plant neu (3 von 6 gleichzeitig)", 250, 5, 5, 25, Rechnet: true);

        Highsanzeige a = Highsanzeige.Bilden(true, false, null, TimeSpan.Zero, Grenze, true, stand);

        Assert.Equal("HiGHS · Teilneuplanung", a.Titel);
        Assert.Equal("plant neu (3 von 6 gleichzeitig)", a.Schritt);
        Assert.Equal("5 von 250 Ausschnitten besser", a.Zeit);
        Assert.False(a.ZeigtAnteil);
        Assert.True(a.Rechnet);
        Assert.All(a.Schritte.Take(5), s => Assert.True(s.Fertig));
        Assert.True(a.Schritte[5].Aktiv);
    }

    [Fact]
    public void Ohne_HiGHS_ein_Hinweis()
    {
        Highsanzeige a = Highsanzeige.Bilden(false, false, null, TimeSpan.Zero, Grenze, false, null);

        Assert.Equal("nicht verfügbar", a.Schritt);
        Assert.False(a.Rechnet);
        Assert.Empty(a.Schritte);
    }
}
