// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

public class ReferenzoptimierungTests
{
    [Theory]
    [InlineData("R2_4__Kreisklasse_Gruppe_A (2).xml", "15")]
    [InlineData("R2_4__Kreisklasse_Gruppe_A (2).xml", "R")]
    [InlineData("R18_Damen_Bezirksliga_Rheinhessen (1).xml", "S1,25")]
    [InlineData("R18_Damen_Bezirksliga_Rheinhessen (1).xml", "M1,25")]
    [InlineData("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml", "5")]
    public void Optimierung_erzeugt_einen_vollstaendigen_gueltigen_Plan(string datei, string strategie)
    {
        Staffel staffel = OhnePlan(Laden(datei));

        Optimierungsergebnis ergebnis = Referenzoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, strategie, 150, startwert: 4711);

        Assert.True(ergebnis.Verbesserungen > 1, "Keine Verbesserung gefunden.");
        Assert.All(ergebnis.Spiele, s => Assert.NotNull(s.Zeitpunkt));

        // Der gefundene Plan, als bestehender Spielplan bewertet, ist gültig und kostet dasselbe wie im Optimierer.
        Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = ergebnis.Spiele }, Berechnungsoptionen.Standard);
        Assert.Equal(0, bewertung.NichtTerminiert);
        Assert.Equal(0, bewertung.UngueltigeSpiele);
        Assert.Equal(0, bewertung.SpieleAnSpielfreienTagen);
        Assert.Equal(Kostenanzeige.Kurz(ergebnis.Kosten), Kostenanzeige.Kurz(bewertung.Gesamtkosten));
    }

    [Fact]
    public void Gleicher_Startwert_ergibt_gleiches_Ergebnis()
    {
        Staffel staffel = OhnePlan(Laden("R2_4__Kreisklasse_Gruppe_A (2).xml"));

        Optimierungsergebnis a = Referenzoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, "5", 60, startwert: 1);
        Optimierungsergebnis b = Referenzoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, "5", 60, startwert: 1);

        Assert.Equal(a.Kosten, b.Kosten);
        Assert.Equal(a.Spiele, b.Spiele);
    }

    [Fact]
    public void Mehr_Durchlaeufe_werden_nicht_schlechter()
    {
        Staffel staffel = OhnePlan(Laden("R2_4__Kreisklasse_Gruppe_A (2).xml"));

        double kurz = Referenzoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, "15", 50, startwert: 7).Kosten;
        double lang = Referenzoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, "15", 200, startwert: 7).Kosten;

        Assert.True(lang <= kurz, $"{lang} > {kurz}");
    }

    private static Staffel OhnePlan(Staffel staffel) => staffel with { BestehenderSpielplan = [] };

    private static Staffel Laden(string datei)
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand);
    }
}
