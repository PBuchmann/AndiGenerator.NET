// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Kostenkachel: prozentuale Verbesserung seit dem ersten gültigen Plan bzw. seit der letzten Kostenanpassung.</summary>
public sealed class GenerierungsanzeigeTests
{
    [Fact]
    public void Verbesserung_bezieht_sich_nach_einer_Kostenanpassung_auf_den_ersten_Plan_danach()
    {
        var anzeige = new Generierungsanzeige();

        anzeige.Uebernehmen(Stand(30_000_000_000));
        Assert.Equal("Plan hat noch harte Fehler", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(1000));
        Assert.Equal("seit dem ersten gültigen Plan unverändert", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(500));
        Assert.Equal("−50 % seit dem ersten gültigen Plan", anzeige.KostenHinweis);

        anzeige.KostenAngepasst();
        anzeige.Uebernehmen(Stand(2000));
        Assert.Equal("seit der letzten Kostenanpassung unverändert", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(1500));
        Assert.Equal("−25 % seit der letzten Kostenanpassung", anzeige.KostenHinweis);
    }

    [Fact]
    public void Eine_Kachel_zeigt_je_nach_Verfahren_Kosten_oder_Verstoesse()
    {
        var anzeige = new Generierungsanzeige();
        Assert.Equal("Gesamtkosten (bester Plan)", anzeige.Titel);
        anzeige.Uebernehmen(Stand(1000));
        Assert.False(anzeige.ZeigtVerstoesse);
        Assert.Equal("Kostenoptimierung · Gesamtkosten", anzeige.Titel);

        var lenkung = new Lenkungsstand(
            new Stufenwert(0, 14, 14, 0, 290, 1_253_211),
            new Stufenwert(0, 3, 4, 2, 316, 3_680_712),
            new Stufenwert(0, 5, 4, 3, 320, 3_900_000),
            "Glätten WechselHeimAuswaerts",
            "Optimierung Stufe C",
            7,
            1,
            Berechnungsoptionen.Standard,
            ["38 s angehoben: ParalleleSpiele TSV A Hoch"],
            [Kostenkriterium.Spielverteilung]);
        anzeige.Uebernehmen(Stand(1000) with { Lenkung = lenkung });

        Assert.True(anzeige.ZeigtVerstoesse);
        Assert.Equal("Automodus · Optimierung Stufe C", anzeige.Titel);
        Assert.Equal(Planqualitaet.Name(Kostenkriterium.Spielverteilung), anzeige.Phase);

        // Alle Kriterien werden genannt, auch viele (unten in der Kachel ist Platz).
        Kostenkriterium[] viele = [Kostenkriterium.Hallenbelegung, Kostenkriterium.ParalleleSpiele, Kostenkriterium.Pflichtspieltage, Kostenkriterium.Sperrtermine];
        anzeige.Uebernehmen(Stand(1000) with { Lenkung = lenkung with { Kriterien = viele } });
        Assert.Equal(string.Join(", ", viele.Select(Planqualitaet.Name)), anzeige.Phase);
        Assert.Equal("14 →", anzeige.BasisA);
        Assert.Equal("14 →", anzeige.BasisB);
        Assert.Equal("290 →", anzeige.BasisC);
        Assert.Equal("erster Plan wird bewertet …", anzeige.KostenHinweis);

        anzeige.Uebernehmen(Stand(900));
        Assert.False(anzeige.ZeigtVerstoesse);
        Assert.Equal("Kostenoptimierung · Gesamtkosten", anzeige.Titel);
        Assert.Equal("–", anzeige.VerstossA);
        Assert.Equal(string.Empty, anzeige.Phase);
        Assert.Equal(string.Empty, anzeige.BasisA);
    }

    [Fact]
    public void Kerne_zeigen_ihre_Last_im_Verhaeltnis_zum_schnellsten()
    {
        var anzeige = new Generierungsanzeige();
        anzeige.Uebernehmen(MitKernen(0, 0));
        anzeige.Uebernehmen(MitKernen(1000, 500));

        Assert.Equal(2, anzeige.Kerne.Count);
        Assert.True(anzeige.Rechnet);
        Assert.Equal(22, anzeige.Kerne[0].Hoehe);
        Assert.True(anzeige.Kerne[1].Hoehe < anzeige.Kerne[0].Hoehe);

        // Angehalten ruhen alle Kerne.
        anzeige.Uebernehmen(MitKernen(1000, 500) with { Pausiert = true });
        Assert.False(anzeige.Rechnet);
        Assert.All(anzeige.Kerne, k => Assert.Equal(3, k.Hoehe));
    }

    [Fact]
    public void Viele_Kerne_stehen_in_der_Kachel_in_Gruppen_und_im_Maschinenraum_als_Raster()
    {
        var anzeige = new Generierungsanzeige();
        anzeige.Uebernehmen(MitKernen(new long[192]));
        Assert.Equal(192, anzeige.Kerne.Count);
        Assert.Equal(16, anzeige.KachelKerne.Count);
        Assert.True(anzeige.VieleKerne);
        Assert.Equal("Kerne 1–12", anzeige.KachelKerne[0].Name);

        anzeige.Uebernehmen(MitKernen(new long[8]));
        Assert.Equal(8, anzeige.KachelKerne.Count);
        Assert.False(anzeige.VieleKerne);
        Assert.Same(anzeige.Kerne[0], anzeige.KachelKerne[0]);
    }

    [Fact]
    public void Verlauf_sammelt_nur_waehrend_gerechnet_wird_und_verdichtet_sich()
    {
        var verlauf = new Suchverlauf();
        verlauf.Hinzufuegen(Stand(1000));
        verlauf.Hinzufuegen(Stand(900) with { Pausiert = true });
        Assert.Single(verlauf.Punkte);

        for (int i = 0; i < Suchverlauf.HoechstePunkte; i++)
        {
            verlauf.Hinzufuegen(Stand(800 - i));
        }

        Assert.True(verlauf.Punkte.Count <= Suchverlauf.HoechstePunkte);
        Assert.Equal(1000, verlauf.Punkte[0].Kosten);
        Assert.Equal(800 - Suchverlauf.HoechstePunkte + 1, verlauf.Punkte[^1].Kosten);
        Assert.True(verlauf.Punkte.Zip(verlauf.Punkte.Skip(1)).All(p => p.First.Sekunden <= p.Second.Sekunden));
    }

    private static Optimierungsstand MitKernen(params long[] kerne) =>
        Stand(1000) with { Maschinenraum = new Maschinenraum(kerne, [], 100_000, null, 0) };

    private static Optimierungsstand Stand(double kosten) =>
        new(true, false, 100, 10, kosten, 1, TimeSpan.Zero, null, []);
}
