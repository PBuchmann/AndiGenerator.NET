// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Differenzielle Tests der schnellen Engine (MIGRATIONSPLAN E7) gegen das Referenzmodell: gleiche Bewertung bis auf das
/// letzte Bit für alle Referenzpläne und viele zufällig veränderte Pläne mit zufälligen Optionen, und gleiche
/// Optimierungsverläufe bei gleichem Startwert.
/// </summary>
public class KernDifferenzTests
{
    private static readonly string[] Strategien = ["R", "15", "5", "S1,25", "M2,10"];

    public static TheoryData<string> Eingaben()
    {
        var daten = new TheoryData<string>();
        foreach (string datei in Directory.GetFiles(Eingabeordner(), "*.xml").Order(StringComparer.Ordinal))
        {
            daten.Add(Path.GetFileName(datei));
        }

        daten.Add(Anreicherung.Name);
        return daten;
    }

    public static TheoryData<string, int> ZufallsplaeneDaten()
    {
        var daten = new TheoryData<string, int>();
        foreach (string datei in Directory.GetFiles(Eingabeordner(), "*.xml").Select(d => Path.GetFileName(d)).Append(Anreicherung.Name).Order(StringComparer.Ordinal))
        {
            for (int startwert = 1; startwert <= 8; startwert++)
            {
                daten.Add(datei, startwert);
            }
        }

        return daten;
    }

    public static TheoryData<string, string, int> VerlaufDaten()
    {
        string[] dateien =
        [
            "R2_4__Kreisklasse_Gruppe_A (2).xml", "R18_Damen_Bezirksliga_Rheinhessen (1).xml", "R3_Damen_Bezirksoberliga_Rheinhessen (1).xml",
            "R23_Liga für Doppelrunde.xml", "R24_Liga mit 11 Mannschaften und 40km Regel.xml", "R25_Liga mit 2 Mannschaften aus einem Verein.xml",
            "R26_Liga mit Koppelterminen.xml", "R21_Verbandsoberliga (1).xml", Anreicherung.Name,
        ];
        var daten = new TheoryData<string, string, int>();
        foreach (string datei in dateien)
        {
            foreach (string strategie in Strategien)
            {
                daten.Add(datei, strategie, 0);
            }

            daten.Add(datei, "15", 3);
            daten.Add(datei, "R", 5);
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(Eingaben))]
    public void Bewertung_des_Originalplans_ist_bitgleich(string datei)
    {
        Staffel staffel = Laden(datei);
        VergleicheBewertung(staffel, Berechnungsoptionen.Standard, datei);
    }

    [Theory]
    [MemberData(nameof(ZufallsplaeneDaten))]
    public void Bewertung_zufaellig_veraenderter_Plaene_ist_bitgleich(string datei, int startwert)
    {
        var zufall = new Random(startwert * 7919);
        Staffel staffel = Laden(datei);
        staffel = staffel with { BestehenderSpielplan = Veraendern(staffel, zufall) };
        Berechnungsoptionen optionen = startwert == 1 ? Berechnungsoptionen.Standard : ZufaelligeOptionen(staffel, zufall);
        VergleicheBewertung(staffel, optionen, $"{datei} / {startwert}");
    }

    [Theory]
    [MemberData(nameof(VerlaufDaten))]
    public void Optimierung_verlaeuft_wie_im_Referenzmodell(string datei, string strategie, int optionsStartwert)
    {
        Staffel staffel = Laden(datei) with { BestehenderSpielplan = [] };
        Berechnungsoptionen optionen = optionsStartwert == 0
            ? Berechnungsoptionen.Standard
            : ZufaelligeOptionen(staffel, new Random(optionsStartwert));

        Optimierungsergebnis referenz = Referenzoptimierung.Optimieren(staffel, optionen, strategie, 40, startwert: 11);
        Optimierungsergebnis kern = Kernoptimierung.Optimieren(staffel, optionen, strategie, 40, startwert: 11);

        Assert.Equal(referenz.Verbesserungen, kern.Verbesserungen);
        Assert.Equal(referenz.Kosten, kern.Kosten);
        Assert.Equal(referenz.Spiele, kern.Spiele);
    }

    [Theory]
    [MemberData(nameof(VerlaufDaten))]
    public void Kosten_mit_Cache_sind_bitgleich_zu_Kosten_ohne_Cache(string datei, string strategie, int optionsStartwert)
    {
        Staffel staffel = Laden(datei) with { BestehenderSpielplan = [] };
        Berechnungsoptionen optionen = optionsStartwert == 0
            ? Berechnungsoptionen.Standard
            : ZufaelligeOptionen(staffel, new Random(optionsStartwert));
        var plan = new KernPlan(RefPlan.Laden(staffel, optionen));
        var zufall = new Zufall(17);
        Tauschstrategie tausch = Tauschstrategie.Lesen(strategie);
        plan.TermineLeeren();
        plan.UngueltigeTermineEntfernen();
        double beste = plan.Kosten();
        plan.Merken();

        for (int durchlauf = 0; durchlauf < 150; durchlauf++)
        {
            plan.Zuruecksetzen();
            plan.VorgegebeneSpieleSetzen();
            plan.NeuWuerfeln(tausch, zufall);
            plan.TermineFuellen(zufall);
            double mitCache = plan.Kosten();
            double ohneCache = plan.KostenOhneCache();
            Assert.True(
                BitConverter.DoubleToInt64Bits(mitCache) == BitConverter.DoubleToInt64Bits(ohneCache),
                $"Durchlauf {durchlauf}: mit Cache {mitCache:R}, ohne Cache {ohneCache:R}");
            if (mitCache < beste)
            {
                plan.Merken();
                beste = mitCache;
            }
        }
    }

    private static void VergleicheBewertung(Staffel staffel, Berechnungsoptionen optionen, string fall)
    {
        Planbewertung? referenz = null;
        Planbewertung? kern = null;
        Exception? referenzFehler = null;
        Exception? kernFehler = null;
        try
        {
            referenz = Referenzbewertung.Bewerten(staffel, optionen);
        }
        catch (Exception e) when (e is InvalidOperationException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            referenzFehler = e;
        }

        try
        {
            kern = Kernbewertung.Bewerten(staffel, optionen);
        }
        catch (Exception e) when (e is InvalidOperationException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            kernFehler = e;
        }

        if (referenzFehler is not null || kernFehler is not null)
        {
            Assert.True(referenzFehler is not null && kernFehler is not null, $"{fall}: nur eine Seite wirft: {referenzFehler?.Message} / {kernFehler?.Message}");
            return;
        }

        var fehler = new List<string>();
        Gleich(fehler, "Gesamtkosten", referenz!.Gesamtkosten, kern!.Gesamtkosten);
        Gleich(fehler, "NichtTerminiert", referenz.NichtTerminiert, kern.NichtTerminiert);
        Gleich(fehler, "UngueltigeSpiele", referenz.UngueltigeSpiele, kern.UngueltigeSpiele);
        Gleich(fehler, "SpieleAnSpielfreienTagen", referenz.SpieleAnSpielfreienTagen, kern.SpieleAnSpielfreienTagen);
        Gleich(fehler, "UeberlappungSpieltage", referenz.UeberlappungSpieltage, kern.UeberlappungSpieltage);
        Gleich(fehler, "LaengeSpieltage", referenz.LaengeSpieltage, kern.LaengeSpieltage);
        Gleich(fehler, "UeberlappungLetzterSpieltag", referenz.UeberlappungLetzterSpieltag, kern.UeberlappungLetzterSpieltag);
        Gleich(fehler, "LaengeLetzterSpieltag", referenz.LaengeLetzterSpieltag, kern.LaengeLetzterSpieltag);
        Gleich(fehler, "VereinsinterneSpieleAmAnfang", referenz.VereinsinterneSpieleAmAnfang, kern.VereinsinterneSpieleAmAnfang);
        Assert.Equal(referenz.SichtbareKostenarten, kern.SichtbareKostenarten);
        Assert.Equal(referenz.Mannschaften.Count, kern.Mannschaften.Count);

        for (int t = 0; t < referenz.Mannschaften.Count; t++)
        {
            Mannschaftsbewertung r = referenz.Mannschaften[t];
            Mannschaftsbewertung k = kern.Mannschaften[t];
            Assert.Equal(r.Name, k.Name);
            Gleich(fehler, $"{r.Name} / Gesamt", r.Gesamt, k.Gesamt);
            for (int a = 0; a < r.JeKostenart.Count; a++)
            {
                if (r.JeKostenart[a] != k.JeKostenart[a])
                {
                    fehler.Add($"{r.Name} / {Referenzbewertung.Kostenartnamen[a]}: Referenz {r.JeKostenart[a]}, Kern {k.JeKostenart[a]}");
                }
            }
        }

        Assert.True(fehler.Count == 0, $"{fall}: {fehler.Count} Abweichung(en):\n" + string.Join("\n", fehler.Take(40)));
    }

    private static void Gleich(List<string> fehler, string was, double referenz, double kern)
    {
        if (BitConverter.DoubleToInt64Bits(referenz) != BitConverter.DoubleToInt64Bits(kern))
        {
            fehler.Add(string.Create(CultureInfo.InvariantCulture, $"{was}: Referenz {referenz:R}, Kern {kern:R}"));
        }
    }

    /// <summary>Verändert den bestehenden Spielplan zufällig: fehlende, verschobene, fremde, vertauschte, doppelte und entfernte Spiele.</summary>
    private static List<Spiel> Veraendern(Staffel staffel, Random zufall)
    {
        List<Spiel> quelle = staffel.BestehenderSpielplan.Count > 0
            ? staffel.BestehenderSpielplan.ToList()
            : staffel.Mannschaften.SelectMany(h => staffel.Mannschaften.Where(g => g.Name != h.Name).Select(g => new Spiel(null, h.Name, g.Name, string.Empty))).ToList();
        DateTime[] alleTermine = staffel.Mannschaften.SelectMany(m => m.Heimspieltermine).Select(h => h.Zeitpunkt).ToArray();
        TimeSpan[] uhrzeiten = [new(10, 0, 0), new(15, 0, 0), new(19, 30, 0), new(20, 0, 0)];
        var ergebnis = new List<Spiel>();
        foreach (Spiel spiel in quelle)
        {
            Mannschaft? heim = staffel.Mannschaften.FirstOrDefault(m => m.Name == spiel.Heim);
            double w = zufall.NextDouble();
            Spiel neu = spiel;
            if (w < 0.10)
            {
                neu = spiel with { Zeitpunkt = null };
            }
            else if (w < 0.40 && heim is { Heimspieltermine.Count: > 0 })
            {
                neu = spiel with { Zeitpunkt = heim.Heimspieltermine[zufall.Next(heim.Heimspieltermine.Count)].Zeitpunkt };
            }
            else if (w < 0.45 && alleTermine.Length > 0)
            {
                neu = spiel with { Zeitpunkt = alleTermine[zufall.Next(alleTermine.Length)] };
            }
            else if (w < 0.50 && spiel.Zeitpunkt is DateTime z)
            {
                neu = spiel with { Zeitpunkt = z.Date.AddDays(zufall.Next(-3, 4)) + uhrzeiten[zufall.Next(uhrzeiten.Length)] };
            }
            else if (w < 0.53)
            {
                neu = spiel with { Heim = spiel.Gast, Gast = spiel.Heim };
            }

            if (w is >= 0.53 and < 0.55)
            {
                continue;
            }

            ergebnis.Add(neu);
            if (w is >= 0.55 and < 0.57)
            {
                ergebnis.Add(neu);
            }
        }

        // Zufällige Reihenfolge: die Zuordnung zu Hin- und Rückspielen hängt von ihr ab.
        return ergebnis.OrderBy(_ => zufall.Next()).ToList();
    }

    private static Berechnungsoptionen ZufaelligeOptionen(Staffel staffel, Random zufall)
    {
        Gewichtung[] werte = Enum.GetValues<Gewichtung>();
        Rundenplanung[] planungen = Enum.GetValues<Rundenplanung>();
        Gewichtung Irgendeine() => zufall.NextDouble() < 0.2 ? Gewichtung.NichtBeruecksichtigen : werte[zufall.Next(werte.Length)];

        var mannschaften = staffel.Mannschaften
            .Where(_ => zufall.NextDouble() < 0.3)
            .Select(m => new MannschaftsGewichtung(m.Id, Irgendeine(), Enumerable.Range(0, Berechnungsoptionen.AnzahlKostenarten).Select(_ => Irgendeine()).ToArray()))
            .ToList();
        return Berechnungsoptionen.Standard with
        {
            FreitagZaehltZumWochenende = zufall.NextDouble() < 0.5,
            Spieltag = Irgendeine(),
            SpieltagUeberlappung = Irgendeine(),
            LetzterSpieltag = Irgendeine(),
            LetzterSpieltagUeberlappung = Irgendeine(),
            VereinsinterneSpieleAmAnfang = Irgendeine(),
            JeKostenart = Enumerable.Range(0, Berechnungsoptionen.AnzahlKostenarten).Select(_ => Irgendeine()).ToArray(),
            Mannschaften = mannschaften,
            Rundenplanung = planungen[zufall.Next(planungen.Length)],
            Doppelrunde = zufall.NextDouble() < 0.3,
        };
    }

    private static Staffel Laden(string datei)
    {
        if (datei == Anreicherung.Name)
        {
            return Anreicherung.Erzeugen();
        }

        string pfad = Path.Combine(Eingabeordner(), datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand);
    }

    private static string Eingabeordner() => Path.Combine(Testdaten.Referenz, "eingabe");
}
