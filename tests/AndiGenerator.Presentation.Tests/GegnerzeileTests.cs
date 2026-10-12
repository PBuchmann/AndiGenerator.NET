// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Seite „Wochenende (60-km-Regel)“: Entfernung und Hinweis je Gegner.</summary>
public sealed class GegnerzeileTests
{
    [Fact]
    public void Weiter_Gegner_ohne_Haekchen_bekommt_einen_Hinweis()
    {
        int meldungen = 0;
        var zeile = new Gegnerzeile(new Hallenentfernung("TTC Fern", 72.4, HeimUnterDerWoche: true), () => meldungen++);

        Assert.Equal("72 km", zeile.Entfernung);
        Assert.True(zeile.HatHinweis);

        zeile.Gewaehlt = true;

        Assert.False(zeile.HatHinweis);
        Assert.Equal(1, meldungen);
    }

    [Fact]
    public void Naher_Gegner_mit_Haekchen_bekommt_einen_Hinweis()
    {
        var zeile = new Gegnerzeile(new Hallenentfernung("TTC Nah", 12, HeimUnterDerWoche: true), () => { });

        zeile.Gewaehlt = true;

        Assert.Equal("nah", zeile.Hinweis);
    }

    [Fact]
    public void Ohne_Adresse_oder_nur_am_Wochenende_kein_Hinweis()
    {
        var ohneAdresse = new Gegnerzeile(new Hallenentfernung("TTC Unbekannt", null, HeimUnterDerWoche: true), () => { });
        var wochenende = new Gegnerzeile(new Hallenentfernung("TTC Fern", 150, HeimUnterDerWoche: false), () => { });

        Assert.Equal((string.Empty, false), (ohneAdresse.Entfernung, ohneAdresse.HatHinweis));
        Assert.False(wochenende.HatHinweis);
    }
}
