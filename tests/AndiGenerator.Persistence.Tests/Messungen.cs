// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Geschwindigkeitsmessungen (Ausgabe in <c>build-messung.txt</c>). Sie prüfen nichts Fachliches und laufen allein,
/// damit andere Tests die Kerne nicht mitbenutzen. Wegen ihrer Laufzeit gehören sie nicht zum normalen Testlauf
/// (<c>bauen.cmd</c>, <c>abdeckung.cmd</c>, CI filtern <c>Kategorie!=Messung</c>), sondern laufen mit <c>messen.cmd</c>.
/// </summary>
[Collection(MessungenSammlung.Name)]
[Trait("Kategorie", "Messung")]
public class Messungen
{
    private static readonly string[] Strategien = ["R", "15", "5", "S1,25", "M2,10"];

    [Fact]
    public async Task Ein_Thread_je_Strategie_messen()
    {
        Staffel staffel = LeereStaffel();
        Kernoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, "15", 200, startwert: 1);
        var zeilen = new List<string>();
        foreach (string strategie in Strategien)
        {
            var uhr = Stopwatch.StartNew();
            const int kernDurchlaeufe = 5000;
            Kernoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, strategie, kernDurchlaeufe, startwert: 2);
            double kernRate = kernDurchlaeufe / uhr.Elapsed.TotalSeconds;

            uhr.Restart();
            OhneCacheOptimieren(staffel, strategie, kernDurchlaeufe);
            double ohneCacheRate = kernDurchlaeufe / uhr.Elapsed.TotalSeconds;

            uhr.Restart();
            const int referenzDurchlaeufe = 300;
            Referenzoptimierung.Optimieren(staffel, Berechnungsoptionen.Standard, strategie, referenzDurchlaeufe, startwert: 2);
            double referenzRate = referenzDurchlaeufe / uhr.Elapsed.TotalSeconds;
            zeilen.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC R2 Strategie {strategie,-6} ein Thread: Kern {kernRate,7:F0}/s (ohne Cache {ohneCacheRate,7:F0}/s), Referenz {referenzRate,5:F0}/s, Faktor {kernRate / referenzRate,5:F1}"));
        }

        await File.AppendAllLinesAsync(Path.Combine(Loesungsordner(), "build-messung.txt"), zeilen);
        Assert.Equal(Strategien.Length, zeilen.Count);
    }

    [Fact]
    public async Task Inselmodell_Skalierung_messen()
    {
        Staffel staffel = LeereStaffel();
        int kerne = Optimierungseinstellungen.Standard.Kerne;
        double einer = await Durchsatz(staffel, 1);
        double alle = await Durchsatz(staffel, kerne);

        string zeile = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC R2 Inselmodell (4 Inseln × 24 Suchplätze, Strategiemix): 1 Thread {einer:F0}/s, {kerne} Threads {alle:F0}/s, Skalierung {alle / einer:F1} ({Environment.ProcessorCount} logische Prozessoren)");
        await File.AppendAllLinesAsync(Path.Combine(Loesungsordner(), "build-messung.txt"), [zeile]);
        Assert.True(alle > 0);
    }

    [Theory]
    [InlineData("eingabe/R2_4__Kreisklasse_Gruppe_A (2).xml")]
    [InlineData("perf/Verbandsoberliga (1).xml")]
    public async Task Phasen_messen(string datei)
    {
        Staffel staffel = LeereStaffel(datei);
        var zeilen = new List<string>();
        foreach (string strategie in new[] { "15", "S1,25", "R" })
        {
            var plan = new KernPlan(RefPlan.Laden(staffel, Berechnungsoptionen.Standard));
            var zufall = new Zufall(3);
            Tauschstrategie tausch = Tauschstrategie.Lesen(strategie);
            plan.TermineLeeren();
            plan.UngueltigeTermineEntfernen();
            double beste = plan.Kosten();
            plan.Merken();
            long[] t = new long[9];
            const int durchlaeufe = 20_000;
            for (int d = 0; d < durchlaeufe; d++)
            {
                long a = Stopwatch.GetTimestamp();
                plan.Zuruecksetzen();
                plan.VorgegebeneSpieleSetzen();
                long b = Stopwatch.GetTimestamp();
                plan.NeuWuerfeln(tausch, zufall);
                long c = Stopwatch.GetTimestamp();
                plan.TermineFuellen(zufall);
                long e = Stopwatch.GetTimestamp();
                double kosten = plan.Kosten();
                long f = Stopwatch.GetTimestamp();
                t[0] += b - a;
                t[1] += c - b;
                t[2] += e - c;
                t[3] += f - e;
                if (kosten < beste)
                {
                    plan.Merken();
                    beste = kosten;
                }

                // Teile der Kostenrechnung getrennt (nur zur Einordnung, zusätzlich gerechnet).
                long g = Stopwatch.GetTimestamp();
                plan.NichtErlaubteAnzahl();
                long h = Stopwatch.GetTimestamp();
                plan.SpieltagsKosten();
                long i = Stopwatch.GetTimestamp();
                plan.FehlterminKosten();
                plan.SpielfreieTageKosten();
                long j = Stopwatch.GetTimestamp();
                t[4] += h - g;
                t[5] += i - h;
                t[6] += j - i;
            }

            double summe = t[0] + t[1] + t[2] + t[3];
            string Anteil(long wert) => (100.0 * wert / summe).ToString("F1", CultureInfo.InvariantCulture) + " %";
            double mikro = 1e6 / Stopwatch.Frequency / durchlaeufe;
            zeilen.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC Phasen {Path.GetFileNameWithoutExtension(datei)} Strategie {strategie,-6}: {summe * mikro:F1} µs/Durchlauf; Zurücksetzen+Vorgaben {Anteil(t[0])}, Neu würfeln {Anteil(t[1])}, Termine füllen {Anteil(t[2])}, Kosten {Anteil(t[3])} (darin ungültige Spiele ~{t[4] * mikro:F1} µs, Spieltage ~{t[5] * mikro:F1} µs, Fehltermine+spielfrei ~{t[6] * mikro:F1} µs)"));
        }

        zeilen.Add(KostenartenMessen(staffel, Path.GetFileNameWithoutExtension(datei)));
        await File.AppendAllLinesAsync(Path.Combine(Loesungsordner(), "build-messung.txt"), zeilen);
        Assert.Equal(4, zeilen.Count);
    }

    /// <summary>Rechenzeit je Kostenart (alle Mannschaften, ohne Cache) über typische Zwischenstände der Strategie 15.</summary>
    private static string KostenartenMessen(Staffel staffel, string name)
    {
        var plan = new KernPlan(RefPlan.Laden(staffel, Berechnungsoptionen.Standard));
        var zufall = new Zufall(5);
        Tauschstrategie tausch = Tauschstrategie.Lesen("15");
        plan.TermineLeeren();
        plan.UngueltigeTermineEntfernen();
        double beste = plan.KostenOhneCache();
        plan.Merken();
        MannschaftsKostenart[] arten = Enum.GetValues<MannschaftsKostenart>();
        long[] zeiten = new long[arten.Length];
        const int durchlaeufe = 3_000;
        for (int d = 0; d < durchlaeufe; d++)
        {
            plan.Zuruecksetzen();
            plan.VorgegebeneSpieleSetzen();
            plan.NeuWuerfeln(tausch, zufall);
            plan.TermineFuellen(zufall);
            foreach (MannschaftsKostenart art in arten)
            {
                long a = Stopwatch.GetTimestamp();
                plan.MannschaftsKosten(art);
                zeiten[(int)art] += Stopwatch.GetTimestamp() - a;
            }

            double kosten = plan.KostenOhneCache();
            if (kosten < beste)
            {
                plan.Merken();
                beste = kosten;
            }
        }

        double mikro = 1e6 / Stopwatch.Frequency / durchlaeufe;
        IEnumerable<string> teile = arten.OrderByDescending(a => zeiten[(int)a])
            .Select(a => string.Create(CultureInfo.InvariantCulture, $"{a} {zeiten[(int)a] * mikro:F2}"));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC Kostenarten {name} (µs je Durchlauf, alle Mannschaften, ohne Cache): {string.Join(", ", teile)}");
    }

    /// <summary>Durchläufe je Sekunde im Parallelbetrieb, gemessen über 3 s nach 1 s Einlaufzeit.</summary>
    private static async Task<double> Durchsatz(Staffel staffel, int kerne)
    {
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = kerne, Startwert = 1 });
        optimierer.Starten();
        await Task.Delay(1000);
        long vorher = optimierer.Durchlaeufe;
        var uhr = Stopwatch.StartNew();
        await Task.Delay(3000);
        double rate = (optimierer.Durchlaeufe - vorher) / uhr.Elapsed.TotalSeconds;
        optimierer.Stoppen();
        return rate;
    }

    /// <summary>Wie <see cref="Kernoptimierung.Optimieren"/>, aber jede Kostenrechnung ohne Cache.</summary>
    private static void OhneCacheOptimieren(Staffel staffel, string strategie, int durchlaeufe)
    {
        var plan = new KernPlan(RefPlan.Laden(staffel, Berechnungsoptionen.Standard));
        var zufall = new Zufall(2);
        Tauschstrategie tausch = Tauschstrategie.Lesen(strategie);
        plan.TermineLeeren();
        plan.UngueltigeTermineEntfernen();
        double beste = plan.KostenOhneCache();
        plan.Merken();
        for (int durchlauf = 0; durchlauf < durchlaeufe; durchlauf++)
        {
            plan.Zuruecksetzen();
            plan.VorgegebeneSpieleSetzen();
            plan.NeuWuerfeln(tausch, zufall);
            plan.TermineFuellen(zufall);
            double kosten = plan.KostenOhneCache();
            if (kosten < beste)
            {
                plan.Merken();
                beste = kosten;
            }
        }
    }

    private static Staffel LeereStaffel(string datei = "eingabe/R2_4__Kreisklasse_Gruppe_A (2).xml")
    {
        string pfad = Path.Combine(Testdaten.Referenz, datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand) with { BestehenderSpielplan = [] };
    }

    private static string Loesungsordner()
    {
        string loesung = typeof(Messungen).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "Loesungsordner").Value!;
        return Path.GetFullPath(loesung);
    }
}
