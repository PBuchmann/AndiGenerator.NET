// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Import;
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

    /// <summary>Zu diesen Zeiten (Sekunden) merkt sich der Automodus den besten Plan für den Verlauf am Ende.</summary>
    private static readonly int[] Verlaufspunkte = [30, 60, 90, 120, 180, 240, 300, 420, 600, 900, 1200, 1800];

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
        Staffel staffel = StaffelLaden(a.Datei, leer: false);
        (Berechnungsoptionen optionen, string herkunft) = OptionenLaden(a.Optionen, staffel);
        if (a.HallenWieOriginal)
        {
            optionen = optionen with { Korrekturen = optionen.Korrekturen with { HallenNachNamen = false } };
            herkunft += ", Hallen wie im Original";
        }

        if (a.Original)
        {
            optionen = optionen with { Korrekturen = Korrekturen.Keine, SpielfreiAmPflichtspieltag = false };
            herkunft += ", wie im Original (ohne Korrekturen)";
        }

        if (a.Rundenplanung is Rundenplanung runde)
        {
            optionen = optionen with { Rundenplanung = runde };
            herkunft += $", Rundenplanung {runde}";
        }

        // Mit leerem Plan: bei „nur Rückrunde“ bleibt die Vorrunde aus der Datei fest (wie in der Oberfläche).
        if (a.Leer)
        {
            staffel = staffel with { BestehenderSpielplan = Vorrunde.Festhalten(staffel, optionen, null) };
        }

        // Wie die Oberfläche: Einteilung der Kriterien aus der Staffel (Qualitätsansicht), sonst die Standard-Einteilung.
        Stufeneinteilung einteilung = Stufeneinteilung.AusText(staffel.Kriterienstufen);
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with
        {
            AutoStufen = einteilung,
            Kerne = a.Kerne ?? Optimierungseinstellungen.Standard.Kerne,
            Inseln = a.Inseln ?? Optimierungseinstellungen.Standard.Inseln,
            Startwert = a.Startwert,
            SpezialInsel = !a.OhneSpezial,
            Automodus = a.Auto,
            StartplanInseln = a.StartInseln,
            AutoSpaetannahme = a.Spaet ?? Optimierungseinstellungen.Standard.AutoSpaetannahme,
        };

        List<Spiel>? start = null;
        if (a.Start is not null)
        {
            start = BewertenBefehl.PlanLaden(staffel, a.Start);
            if (start is null)
            {
                return 2;
            }
        }

        // Automodus mit HiGHS wie in der Oberfläche: Schranke und Plan werden nach dem Start eingespielt (ab Ende des Grundlaufs);
        // der Startplan ist (wie dort der beste Plan der Generierung) auch Startlösung der Schranke.
        Schrankenbericht? highs = null;
        if (a.Highs && !a.Nebenher)
        {
            highs = Ligaschranke.Berechnen(staffel, optionen, new SchrankeBefehl.Highsschranke(), Ligaschranke.StandardZeitJeStufe, einteilung, startplan: () => start, laufzeit: SchrankeBefehl.Laufzeit);
            string b = (highs.SchrankeB, highs.SummeB) switch
            {
                ({ } sb, _) => sb.ToString(CultureInfo.InvariantCulture),
                (null, { Gefunden: int g } unbewiesen) => string.Create(CultureInfo.InvariantCulture, $"{unbewiesen.Schranke} (nicht bewiesen, bester Plan {g})"),
                _ => "?",
            };
            Console.WriteLine($"HiGHS:        A ≥ {highs.SchrankeA?.ToString(CultureInfo.InvariantCulture) ?? "?"}, B ≥ {b}" + (highs.Plan.Count > 0 ? string.Empty : " (kein Plan)"));
            foreach (string hinweis in highs.Hinweise.Where(h => h.Contains("kennt das Modell nicht", StringComparison.Ordinal)))
            {
                Console.WriteLine($"HiGHS:        {hinweis}");
            }
        }

        if (start is not null)
        {
            einstellungen = einstellungen with { Startplan = start };
            Console.WriteLine($"Startplan:    {a.Start} für {Math.Min(a.StartInseln, einstellungen.Inseln)} Insel(n)");
        }

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

        if (highs is not null)
        {
            optimierer.HighsEinspielen(highs.Plan, highs.SchrankeA, highs.SchrankeB);
        }

        // Automodus mit Teilneuplanung (HiGHS) nebenher, fortlaufend; mit --nebenher wartet sie wie in der Oberfläche, bis die
        // Untergrenze Stufe A fertig hat (die HiGHS-Linien nicht).
        var vorrang = new System.Runtime.CompilerServices.StrongBox<bool>(a.Nebenher);
        using Teilneuplaner? teilneuplaner = a.Neuplanen && a.Auto && HighsLoeser.Verfuegbar
            ? new Teilneuplaner(
                optimierer,
                staffel,
                optionen,
                new SchrankeBefehl.Highsschranke(),
                einteilung.Vervollstaendigt(),
                warten: () => Volatile.Read(ref vorrang.Value),
                linienloeser: lauf => new SchrankeBefehl.Highsschranke(new Highsvorgabe(lauf + 1, (lauf % 3) switch { 0 => 0.05, 1 => 0.2, _ => 0.5 }, OhneNeustart: true)),
                linien: a.Linien ?? StandardLinien())
            : null;
        Task? nebenher = null;
        if (a.Nebenher)
        {
            var nebenUhr = Stopwatch.StartNew();
            nebenher = Task.Run(() =>
            {
                Schrankenbericht bericht = Ligaschranke.Berechnen(
                    staffel,
                    optionen,
                    new SchrankeBefehl.Highsschranke(),
                    Ligaschranke.StandardZeitJeStufe,
                    einteilung,
                    f =>
                    {
                        if (f.AFertig)
                        {
                            Volatile.Write(ref vorrang.Value, false);
                        }

                        NebenherMelden(optimierer, f, nebenUhr);
                    },
                    startplan: () => optimierer.BesterStand().Spiele,
                    laufzeit: SchrankeBefehl.Laufzeit);
                Console.WriteLine($"  [{nebenUhr.Elapsed.TotalSeconds:0} s] HiGHS: B-Varianten nachbessern");
                bericht = Ligaschranke.VariantenNachbessern(staffel, optionen, new SchrankeBefehl.Highsschranke(), einteilung, bericht, TimeSpan.FromSeconds(30));
                Console.WriteLine($"  [{nebenUhr.Elapsed.TotalSeconds:0} s] HiGHS fertig: A ≥ {bericht.SchrankeA?.ToString(CultureInfo.InvariantCulture) ?? "?"}, B ≥ {bericht.SchrankeB?.ToString(CultureInfo.InvariantCulture) ?? "?"}");
                foreach (string hinweis in bericht.Hinweise)
                {
                    Console.WriteLine($"  HiGHS-Hinweis: {hinweis}");
                }

                foreach (Schrankenstufe st in bericht.Stufen.Where(x => x.Kriterium is null))
                {
                    Console.WriteLine($"  HiGHS-Stufe {st.Stufe} {st.Name}: Grenze {st.Schranke}, gefunden {st.Gefunden?.ToString(CultureInfo.InvariantCulture) ?? "-"}, bewiesen {st.Bewiesen}, {st.Sekunden:0} s");
                }

                Console.WriteLine($"  HiGHS-Summe A: {bericht.SummeA}; Summe B: {bericht.SummeB}");
                foreach (Planalternative alternative in bericht.Alternativen ?? [])
                {
                    Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = alternative.Plan }, optionen);
                    Console.WriteLine($"  HiGHS-Variante {alternative.Name}: {einteilung.Zaehlen(bewertung)}");
                }

                BerichtEinspielen(optimierer, bericht);
                Volatile.Write(ref vorrang.Value, false);
            });
        }

        teilneuplaner?.Starten();

        var uhr = Stopwatch.StartNew();
        var verlauf = new List<string>();
        if (a.Durchlaeufe is long ziel && einstellungen.Kerne == 1)
        {
            optimierer.Rechnen(ziel);
        }
        else
        {
            await ImHintergrundRechnen(optimierer, a, uhr, protokoll, teilneuplaner, verlauf);
        }

        if (nebenher is not null)
        {
            Console.WriteLine(nebenher.IsCompleted ? "HiGHS war fertig." : "HiGHS rechnete noch.");
        }

        await Status(optimierer, uhr, protokoll);
        Console.WriteLine();
        Zusammenfassung(staffel, optionen, optimierer, uhr);
        if (optimierer.Lenkung is { } lenkung)
        {
            Console.WriteLine($"Automodus: bester Plan {lenkung.Bester}");
            if (verlauf.Count > 0)
            {
                // Wie früh ein guter Plan da ist: der beste Plan (A·B·C) zu festen Zeiten.
                Console.WriteLine("Verlauf: " + string.Join("  ", verlauf));
            }

            foreach (string aenderung in lenkung.Aenderungen)
            {
                Console.WriteLine("  " + aenderung);
            }

            foreach (Startstatistik neustart in optimierer.Maschinenraum.Starts ?? [])
            {
                Console.WriteLine($"Neustarts {neustart.Verfahren}: {neustart.Starts}, danach bester Plan verbessert: {neustart.Erfolge}");
            }
        }

        if (a.Plan is not null)
        {
            GespeicherterPlanDatei.Speichern(a.Plan, optimierer.BesterStand().Spiele.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
            Console.WriteLine($"Plan gespeichert: {a.Plan}");
        }

        return 0;
    }

    /// <summary>Wie die Oberfläche: Zwischenbericht (A und B fertig) einspielen, den Plan des letzten Schritts sofort anbieten.</summary>
    private static void NebenherMelden(Inseloptimierer optimierer, Schrankenfortschritt f, Stopwatch uhr)
    {
        if (f.NurPlan)
        {
            if (f.Plan is { Count: > 0 } neu)
            {
                optimierer.PlanEinspielen(neu, $"HiGHS ({f.Text})");
            }

            return;
        }

        Console.WriteLine($"  [{uhr.Elapsed.TotalSeconds:0} s] HiGHS: {f.Text}");
        if (f.Zwischenbericht is { } bericht)
        {
            BerichtEinspielen(optimierer, bericht);
        }

        if (f.SchrankeA is int a)
        {
            optimierer.HighsEinspielen([], a, null);
        }

        if (f.Plan is { Count: > 0 } plan)
        {
            bool besser = optimierer.PlanEinspielen(plan, $"Zwischenplan aus HiGHS (vor {f.Text})", inselnNachziehen: true);
            Console.WriteLine($"  [{uhr.Elapsed.TotalSeconds:0} s] Zwischenplan vor {f.Text}: " + (besser ? "bester Plan" : "nicht übernommen"));
        }
    }

    /// <summary>Wie die Oberfläche: Schranke und Plan einspielen, die Varianten anbieten.</summary>
    private static void BerichtEinspielen(Inseloptimierer optimierer, Schrankenbericht bericht)
    {
        optimierer.HighsEinspielen(bericht.Plan, bericht.SchrankeA, bericht.SchrankeB);
        foreach (Planalternative alternative in bericht.Alternativen ?? [])
        {
            optimierer.PlanEinspielen(alternative.Plan, "Alternative aus HiGHS", inselnNachziehen: true);
        }
    }

    /// <summary>HiGHS-Linien wie in der Oberfläche: zwei ab acht logischen Prozessoren, sonst eine.</summary>
    /// <summary>Wie im Programm (Stufenschranke.Linien): zwei ab zwölf logischen Prozessoren, eine ab acht, darunter keine.</summary>
    private static int StandardLinien() => Environment.ProcessorCount switch
    {
        >= 12 => 2,
        >= 8 => 1,
        _ => 0,
    };

    private static async Task ImHintergrundRechnen(Inseloptimierer optimierer, OptimierenArgumente a, Stopwatch uhr, StreamWriter? protokoll, Teilneuplaner? teilneuplaner, List<string> verlauf)
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
        int naechsterPunkt = 0;
        try
        {
            while (!Fertig(optimierer, a, uhr))
            {
                await Task.Delay(200, abbruch.Token);
                if (naechsterPunkt < Verlaufspunkte.Length && uhr.Elapsed.TotalSeconds >= Verlaufspunkte[naechsterPunkt])
                {
                    if (optimierer.Lenkung is { } l)
                    {
                        verlauf.Add(string.Create(CultureInfo.InvariantCulture, $"{Verlaufspunkte[naechsterPunkt] / 60}:{Verlaufspunkte[naechsterPunkt] % 60:00} {l.Bester.A}·{l.Bester.B}·{l.Bester.CAnzahl}"));
                    }

                    naechsterPunkt++;
                }

                // Vorrang für HiGHS wie in der Oberfläche: So viele Rechen-Threads der Inseln ruhen, wie HiGHS Rechnungen laufen hat.
                optimierer.Ruhend = a.Auto ? Highsauslastung.Laufend : 0;
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

        // Erst die Teilneuplanung beenden: Danach spielt sie nichts mehr ein, Ausgabe und gespeicherter Plan stimmen überein.
        teilneuplaner?.Dispose();
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
        if (optimierer.Lenkung is { } lenkung)
        {
            // Im Automodus: der beste Plan und je Insel ihr bester Plan nach Stufen (womit sie zuletzt neu begonnen hat).
            static string Insel(Inselstand i)
            {
                string start = i.Start is { } st ? " " + st : string.Empty;
                if (i.Verstoesse is not { } w)
                {
                    return "–";
                }

                // Harte Fehler (z. B. Spiele ohne Termin) vorn: Ein Plan mit weniger A, aber harten Fehlern ist nicht besser.
                string harte = w.Harte > 0 ? $"H{w.Harte}·" : string.Empty;
                return $"{harte}{w.A}·{w.B}·{w.CAnzahl}{start}";
            }

            IEnumerable<string> inseln = optimierer.Maschinenraum.Inseln.Where(i => !i.Spezial).Select(Insel);
            Console.WriteLine($"         bester {lenkung.Bester.A}·{lenkung.Bester.B}·{lenkung.Bester.CAnzahl} (Verbesserungen: Inseln {lenkung.VonInseln}, HiGHS {lenkung.VonHighs}; {Highsauslastung.Laufend} HiGHS-Rechnungen) | Inseln {string.Join("  ", inseln)}");
        }

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
