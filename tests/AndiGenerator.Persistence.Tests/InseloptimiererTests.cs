// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Tests des Inselmodells (MIGRATIONSPLAN E6).</summary>
public class InseloptimiererTests
{
    private const string R2 = "R2_4__Kreisklasse_Gruppe_A (2).xml";

    [Fact]
    public void Rechnen_ist_mit_Startwert_reproduzierbar()
    {
        Staffel staffel = LeereStaffel(R2);
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 42 };

        Optimierungsergebnis a = Rechnen(staffel, einstellungen, 20_000);
        Optimierungsergebnis b = Rechnen(staffel, einstellungen, 20_000);

        Assert.Equal(a.Kosten, b.Kosten);
        Assert.Equal(a.Verbesserungen, b.Verbesserungen);
        Assert.Equal(a.Spiele, b.Spiele);
    }

    [Theory]
    [InlineData(R2)]
    [InlineData("R26_Liga mit Koppelterminen.xml")]
    [InlineData("R25_Liga mit 2 Mannschaften aus einem Verein.xml")]
    [InlineData("R24_Liga mit 11 Mannschaften und 40km Regel.xml")]
    public void Rechnen_liefert_einen_gueltigen_besseren_Plan(string datei)
    {
        Staffel staffel = LeereStaffel(datei);
        double start = Kernbewertung.Bewerten(staffel, Berechnungsoptionen.Standard).Gesamtkosten;

        Optimierungsergebnis ergebnis = Rechnen(staffel, Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 7 }, 30_000);

        Assert.True(ergebnis.Kosten < start, $"{ergebnis.Kosten} >= {start}");
        PlanPruefen(staffel, ergebnis);
    }

    [Fact]
    public void Schlechteste_Insel_wird_neu_gestartet()
    {
        Staffel staffel = LeereStaffel(R2);
        using var optimierer = new Inseloptimierer(
            staffel,
            Berechnungsoptionen.Standard,
            Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 3, DurchlaeufeVorNeustart = 2_000 });

        optimierer.Rechnen(150_000);

        Assert.True(optimierer.Neustarts > 0);
        PlanPruefen(staffel, optimierer.BesterStand());
    }

    [Fact]
    public void Spezial_Insel_startet_und_ihre_Loesung_startet_Inseln_neu()
    {
        Staffel staffel = LeereStaffel(R2);
        using var optimierer = new Inseloptimierer(
            staffel,
            Berechnungsoptionen.Standard,
            Optimierungseinstellungen.Standard with
            {
                Kerne = 1,
                Startwert = 5,
                DurchlaeufeVorNeustart = 2_000,
                DurchlaeufeVorSpezialInsel = 5_000,
                MindestDurchlaeufeSpezialInsel = 3_000,
            });

        optimierer.Rechnen(200_000);

        Assert.NotNull(optimierer.SpezialKostenart);
        Assert.True(optimierer.SpezialVerwendet > 0);
        PlanPruefen(staffel, optimierer.BesterStand());
    }

    [Fact]
    public async Task Parallelbetrieb_liefert_einen_gueltigen_Plan()
    {
        Staffel staffel = LeereStaffel(R2);
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 2 });

        optimierer.Starten();
        await Task.Delay(1500);
        optimierer.Stoppen();

        Assert.Null(optimierer.Fehler);
        Assert.True(optimierer.Durchlaeufe > 0);
        PlanPruefen(staffel, optimierer.BesterStand());
    }

    [Fact]
    public async Task Pause_haelt_die_Rechnung_an()
    {
        using var optimierer = new Inseloptimierer(LeereStaffel(R2), Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 2 });
        optimierer.Starten();
        await Task.Delay(300);

        optimierer.Pausiert = true;
        await Task.Delay(300);
        long angehalten = optimierer.Durchlaeufe;
        await Task.Delay(300);
        Assert.Equal(angehalten, optimierer.Durchlaeufe);

        optimierer.Pausiert = false;
        await Task.Delay(300);
        Assert.True(optimierer.Durchlaeufe > angehalten);
    }

    private static Optimierungsergebnis Rechnen(Staffel staffel, Optimierungseinstellungen einstellungen, long durchlaeufe)
    {
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen);
        optimierer.Rechnen(durchlaeufe);
        return optimierer.BesterStand();
    }

    /// <summary>Der gefundene Plan, als bestehender Spielplan bewertet, ist gültig und kostet dasselbe wie im Optimierer.</summary>
    private static void PlanPruefen(Staffel staffel, Optimierungsergebnis ergebnis)
    {
        Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = ergebnis.Spiele }, Berechnungsoptionen.Standard);
        Assert.Equal(0, bewertung.UngueltigeSpiele);
        Assert.Equal(0, bewertung.SpieleAnSpielfreienTagen);
        Assert.Equal(Kostenanzeige.Kurz(ergebnis.Kosten), Kostenanzeige.Kurz(bewertung.Gesamtkosten));
    }

    private static Staffel LeereStaffel(string datei)
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand) with { BestehenderSpielplan = [] };
    }
}
