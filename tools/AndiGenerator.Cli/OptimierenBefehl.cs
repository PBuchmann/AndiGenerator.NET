// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Optionen;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen optimieren</c>: lädt eine Staffel, optimiert mit dem Inselmodell und protokolliert Kosten und Pläne/s
/// (Vergleich von Geschwindigkeit und Ergebnisqualität mit dem Original, MIGRATIONSPLAN Phase 3).
/// </summary>
internal static class OptimierenBefehl
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static async Task<int> Ausfuehren(IReadOnlyList<string> args)
    {
        OptimierenArgumente? a = OptimierenArgumente.Lesen(args, out string fehler);
        if (a is null)
        {
            if (fehler.Length > 0)
            {
                await Console.Error.WriteLineAsync(fehler);
            }

            Console.WriteLine(OptimierenArgumente.Hilfe);
            return 1;
        }

        TextWriter konsole = Console.Out;
        await using StreamWriter? ausgabe = a.Ausgabe is null ? null : new StreamWriter(a.Ausgabe, append: false);
        if (ausgabe is not null)
        {
            Console.SetOut(new DoppelSchreiber(konsole, ausgabe));
        }

        try
        {
            return await Optimieren(a);
        }
        finally
        {
            Console.SetOut(konsole);
        }
    }

    internal static Staffel StaffelLaden(string datei, bool leer)
    {
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(datei);
        string modifikationen = Path.ChangeExtension(datei, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
            Console.WriteLine($"Änderungen:   {modifikationen}");
        }

        Staffel staffel = StaffelAbbildung.AusPlanDaten(stand);
        return leer ? staffel with { BestehenderSpielplan = [] } : staffel;
    }

    internal static (Berechnungsoptionen Optionen, string Herkunft) OptionenLaden(string? angabe, Staffel staffel)
    {
        if (angabe is null)
        {
            return (Berechnungsoptionen.Standard, "Standard");
        }

        string pfad = angabe == "auto"
            ? Path.Combine(Staffelordner.Pfad(Staffelordner.StandardBasis(), staffel.Name, staffel.Beginn), OptionenDatei.Dateiname)
            : angabe;
        return File.Exists(pfad) ? (OptionenDatei.Laden(pfad), pfad) : (Berechnungsoptionen.Standard, $"Standard ({pfad} nicht gefunden)");
    }

    private static async Task<int> Optimieren(OptimierenArgumente a)
    {
        Staffel staffel = StaffelLaden(a.Datei, a.Leer);
        (Berechnungsoptionen optionen, string herkunft) = OptionenLaden(a.Optionen, staffel);
        if (a.Rundenplanung is Rundenplanung runde)
        {
            optionen = optionen with { Rundenplanung = runde };
            herkunft += $", Rundenplanung {runde}";
        }

        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with
        {
            Kerne = a.Kerne ?? Optimierungseinstellungen.Standard.Kerne,
            Startwert = a.Startwert,
            SpezialInsel = !a.OhneSpezial,
        };

        Console.WriteLine($"Staffel:      {staffel.Name} ({staffel.Mannschaften.Count} Mannschaften, bestehender Plan {staffel.BestehenderSpielplan.Count(s => s.Zeitpunkt is not null)} Spiele terminiert)");
        Console.WriteLine($"Optionen:     {herkunft}");
        Console.WriteLine($"Threads:      {einstellungen.Kerne} von {Environment.ProcessorCount} logischen Prozessoren, {einstellungen.Inseln} Inseln, Spezial-Insel {(einstellungen.SpezialInsel ? "an" : "aus")}, Startwert {a.Startwert?.ToString(CultureInfo.InvariantCulture) ?? "zufällig"}");

        using var optimierer = new Inseloptimierer(staffel, optionen, einstellungen);
        Console.WriteLine($"Startkosten:  {Kostenanzeige.Kurz(optimierer.BesterStand().Kosten)}");
        Console.WriteLine();
        Console.WriteLine("    Zeit     Durchläufe     Pläne/s       Kosten  Verb.  Neust.  Spezial");

        await using StreamWriter? protokoll = a.Protokoll is null ? null : new StreamWriter(a.Protokoll, append: false);
        if (protokoll is not null)
        {
            await protokoll.WriteLineAsync("Sekunden;Durchlaeufe;PlaeneProSekunde;Kosten;Verbesserungen;Neustarts;Spezial");
        }

        var uhr = Stopwatch.StartNew();
        if (a.Durchlaeufe is long ziel && einstellungen.Kerne == 1)
        {
            optimierer.Rechnen(ziel);
        }
        else
        {
            await ImHintergrundRechnen(optimierer, a, uhr, protokoll);
        }

        await Status(optimierer, uhr, protokoll);
        Console.WriteLine();
        Zusammenfassung(staffel, optionen, optimierer, uhr);

        if (a.Plan is not null)
        {
            GespeicherterPlanDatei.Speichern(a.Plan, optimierer.BesterStand().Spiele.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
            Console.WriteLine($"Plan gespeichert: {a.Plan}");
        }

        return 0;
    }

    private static async Task ImHintergrundRechnen(Inseloptimierer optimierer, OptimierenArgumente a, Stopwatch uhr, StreamWriter? protokoll)
    {
        using var abbruch = new CancellationTokenSource();
        ConsoleCancelEventHandler strgC = (_, e) =>
        {
            e.Cancel = true;
            abbruch.Cancel();
        };
        Console.CancelKeyPress += strgC;

        optimierer.Starten();
        TimeSpan naechsterStatus = TimeSpan.FromSeconds(a.Intervall);
        try
        {
            while (!Fertig(optimierer, a, uhr))
            {
                await Task.Delay(200, abbruch.Token);
                if (uhr.Elapsed >= naechsterStatus)
                {
                    await Status(optimierer, uhr, protokoll);
                    naechsterStatus += TimeSpan.FromSeconds(a.Intervall);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Abgebrochen.");
        }
        finally
        {
            Console.CancelKeyPress -= strgC;
        }

        optimierer.Stoppen();
        if (optimierer.Fehler is Exception ex)
        {
            await Console.Error.WriteLineAsync("Fehler in der Optimierung: " + ex);
        }
    }

    private static bool Fertig(Inseloptimierer optimierer, OptimierenArgumente a, Stopwatch uhr) =>
        optimierer.Fehler is not null
        || (a.Durchlaeufe is long ziel ? optimierer.Durchlaeufe >= ziel : uhr.Elapsed.TotalSeconds >= a.Sekunden);

    private static async Task Status(Inseloptimierer optimierer, Stopwatch uhr, StreamWriter? protokoll)
    {
        Optimierungsergebnis stand = optimierer.BesterStand();
        double sekunden = uhr.Elapsed.TotalSeconds;
        long durchlaeufe = optimierer.Durchlaeufe;
        double rate = sekunden > 0 ? durchlaeufe / sekunden : 0;
        string spezial = optimierer.SpezialKostenart?.ToString() ?? "-";
        Console.WriteLine(string.Create(
            Deutsch,
            $"{uhr.Elapsed:hh\\:mm\\:ss} {durchlaeufe,14:N0} {rate,11:N0} {Kostenanzeige.Kurz(stand.Kosten),12} {stand.Verbesserungen,6} {optimierer.Neustarts,7}  {spezial}"));
        if (protokoll is not null)
        {
            await protokoll.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"{sekunden:F1};{durchlaeufe};{rate:F0};{stand.Kosten:R};{stand.Verbesserungen};{optimierer.Neustarts};{spezial}"));
            await protokoll.FlushAsync();
        }
    }

    private static void Zusammenfassung(Staffel staffel, Berechnungsoptionen optionen, Inseloptimierer optimierer, Stopwatch uhr)
    {
        Optimierungsergebnis stand = optimierer.BesterStand();
        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = stand.Spiele }, optionen);
        Console.WriteLine(string.Create(Deutsch, $"Laufzeit {uhr.Elapsed:hh\\:mm\\:ss}, {optimierer.Durchlaeufe:N0} Durchläufe, {optimierer.Durchlaeufe / Math.Max(0.001, uhr.Elapsed.TotalSeconds):N0} Pläne/s"));
        Console.WriteLine($"Gesamtkosten:                  {Kostenanzeige.Kurz(b.Gesamtkosten)}");
        Zeile("Spiele nicht terminiert", b.NichtTerminiert);
        Zeile("ungültige Spiele", b.UngueltigeSpiele);
        Zeile("Spiele an spielfreien Tagen", b.SpieleAnSpielfreienTagen);
        Zeile("Überlappung Spieltage", b.UeberlappungSpieltage);
        Zeile("Länge Spieltage", b.LaengeSpieltage);
        Zeile("Überlappung letzter Spieltag", b.UeberlappungLetzterSpieltag);
        Zeile("Länge letzter Spieltag", b.LaengeLetzterSpieltag);
        Zeile("Vereinsinterne Spiele am Anf.", b.VereinsinterneSpieleAmAnfang);
        Console.WriteLine("Kostenarten (Anzahl über alle Mannschaften, Kosten):");
        foreach (MannschaftsKostenart art in b.SichtbareKostenarten)
        {
            int anzahl = b.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)art].Anzahl));
            double kosten = b.Mannschaften.Sum(m => m.JeKostenart[(int)art].Kosten);
            Console.WriteLine($"  {Referenzbewertung.Kostenartnamen[(int)art],-22} {anzahl,5}  {Kostenanzeige.Kurz(kosten),10}");
        }
    }

    private static void Zeile(string name, double wert) => Console.WriteLine($"  {name,-30} {Kostenanzeige.Kurz(wert)}");
}
