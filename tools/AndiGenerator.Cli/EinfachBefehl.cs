// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen einfach</c>: der einfache Modus (<see cref="Schnellplaner"/>) ohne Oberfläche – Phasen, Proben, das Rennen der
/// Kandidaten und der beste Plan nach dem festen Maßstab, zum Messen auf den Referenzstaffeln.
/// </summary>
internal static class EinfachBefehl
{
    public const string Hilfe = """
        Aufruf: andigen einfach <click-TT-Exportdatei.xml> [Optionen]
          --sekunden N      Laufzeit in Sekunden (Standard 600)
          --kerne N         Rechen-Threads (Standard: Prozessorkerne - 1)
          --startwert N     Startwert der Zufallsfolgen
          --optionen DATEI  Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --rundenplanung R beide | halbrunde | vorrunde | rueckrunde
          --vorglaetten N   so lange glättet die Grundeinteilung mit allen Kernen vor dem Rennen (Standard 45)
          --runde N         Rechenzeit je Kandidat in der ersten Runde des Rennens (Standard 30)
          --probe N         Proben der C-Kriterien mit HiGHS, je N Sekunden (Standard 0 = keine)
          --gewicht G=W,…   Gewicht je Gruppe, z. B. heim=5,abst=1 (Gruppen: Heim/Auswärts, Abstände, Spielverteilung, Spieltage, …)
          --faktor K=F,…    Wichtigkeit je Kriterium als Faktor, z. B. Setzliste=6,WechselHeimAuswaerts=0.15 (1 = normal)
          --fokus G         Fokus auf eine Gruppe (wie „Verbessern“ in der Oberfläche)
          --fokus-ab N      den Fokus erst nach N Sekunden setzen (Steuerung während des Laufs)
          --wahl S:V:H;…    mitbestimmen wie in der Oberfläche: nach S Sekunden Vorschlag V (ab 1) wählen, Hebel H
                            (Gruppe wie bei --gewicht, „-“ = keiner), z. B. 300:2:heim;480:1:-
          --mitbestimmen    im Dialog: Enter zeigt Beweisstand und Vorschläge, „2“ wählt Vorschlag 2, „2a“ dazu den Hebel a,
                            „x“ beendet (ohne --sekunden rechnet es bis „x“)
          --plan DATEI      besten Plan als gemerkten Plan speichern
          --zwischen S,S,…  mit --plan: auch den besten Plan nach S Sekunden speichern („<Plan> 60s.xml“ …)
          --ausgabe DATEI   Bildschirmausgabe zusätzlich in diese Datei schreiben
        """;

    public static async Task<int> Ausfuehren(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.WriteLine(Hilfe);
            return 1;
        }

        string datei = args[0];
        var werte = new Dictionary<string, string>(StringComparer.Ordinal);
        int i = 1;
        while (i < args.Count)
        {
            // Schalter ohne Wert (z. B. --mitbestimmen) gelten als „ja“.
            bool mitWert = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            werte[args[i]] = mitWert ? args[i + 1] : "ja";
            i += mitWert ? 2 : 1;
        }

        TextWriter konsole = Console.Out;
        await using StreamWriter? ausgabe = werte.TryGetValue("--ausgabe", out string? aus) ? new StreamWriter(aus, append: false) : null;
        if (ausgabe is not null)
        {
            Console.SetOut(new DoppelSchreiber(konsole, ausgabe));
        }

        try
        {
            return await Planen(datei, werte);
        }
        finally
        {
            Console.SetOut(konsole);
        }
    }

    /// <summary>Die Datei eines Zwischenstands: „Plan 60s.xml“ neben dem Plan.</summary>
    /// <param name="plan">Die Plandatei.</param>
    /// <param name="sekunden">Nach so vielen Sekunden.</param>
    /// <returns>Der Pfad.</returns>
    internal static string Zwischendatei(string plan, int sekunden) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(plan)) ?? ".",
        string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileNameWithoutExtension(plan)} {sekunden}s.xml"));

    private static async Task<int> Planen(string datei, Dictionary<string, string> werte)
    {
        Staffel staffel = OptimierenBefehl.StaffelLaden(datei, leer: false);
        (Berechnungsoptionen optionen, string herkunft) = OptimierenBefehl.OptionenLaden(werte.GetValueOrDefault("--optionen"), staffel);
        if (werte.TryGetValue("--rundenplanung", out string? r) && Rundenwahl.Lesen(r) is Rundenplanung runde)
        {
            optionen = optionen with { Rundenplanung = runde };
            herkunft += $", Rundenplanung {runde}";
        }

        staffel = staffel with { BestehenderSpielplan = Vorrunde.Festhalten(staffel, optionen, null) };
        int Zahl(string name, int standard) => werte.TryGetValue(name, out string? text) ? int.Parse(text, CultureInfo.InvariantCulture) : standard;
        Schnellplanereinstellungen e = Schnellplanereinstellungen.Standard with
        {
            Kerne = Zahl("--kerne", Schnellplanereinstellungen.Standard.Kerne),
            ErsteRunde = TimeSpan.FromSeconds(Zahl("--runde", 30)),
            Vorglaetten = TimeSpan.FromSeconds(Zahl("--vorglaetten", 45)),
            ZeitJeProbe = TimeSpan.FromSeconds(Zahl("--probe", 0)),
            Startwert = werte.ContainsKey("--startwert") ? Zahl("--startwert", 0) : null,
        };
        bool mitbestimmen = werte.ContainsKey("--mitbestimmen");

        int sekunden = Zahl("--sekunden", mitbestimmen ? 6 * 3600 : 600);
        Console.WriteLine($"Staffel:      {staffel.Name} ({staffel.Mannschaften.Count} Mannschaften)");
        Console.WriteLine($"Optionen:     {herkunft}");
        Console.WriteLine($"Threads:      {e.Kerne} von {Environment.ProcessorCount} logischen Prozessoren, Laufzeit {sekunden} s");
        Console.WriteLine();

        // Steuerung wie später in der Oberfläche: Gewichte sofort, Fokus sofort oder ab einer Sekunde („--fokus-ab“).
        var steuerung = new Schnellsteuerung();
        foreach (string paar in werte.GetValueOrDefault("--gewicht", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] teile = paar.Split('=');
            steuerung.Gewicht(Gruppe(teile[0]), double.Parse(teile[1], CultureInfo.InvariantCulture));
        }

        foreach (string paar in werte.GetValueOrDefault("--faktor", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] teile = paar.Split('=');
            steuerung.Wichtigkeit(Enum.Parse<Kostenkriterium>(teile[0], ignoreCase: true), double.Parse(teile[1], CultureInfo.InvariantCulture));
        }

        int? fokus = werte.TryGetValue("--fokus", out string? f) ? Gruppe(f) : null;
        using var fokusTimer = new Timer(_ => steuerung.Fokus(fokus), null, fokus is null ? Timeout.Infinite : Zahl("--fokus-ab", 0) * 1000, Timeout.Infinite);
        using var abbruch = new CancellationTokenSource(TimeSpan.FromSeconds(sekunden));
        string? zuletzt = null;
        Schnellstand? letzterStand = null;
        var lockStand = new Lock();
        List<Timer> wahlen = [.. werte.GetValueOrDefault("--wahl", string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries).Select(teil =>
        {
            string[] t = teil.Split(':');
            int nummer = int.Parse(t[1], CultureInfo.InvariantCulture);
            int? hebel = t.Length > 2 && t[2] != "-" ? Gruppe(t[2]) : null;
            return new Timer(
                _ =>
                {
                    Schnellstand? stand;
                    lock (lockStand)
                    {
                        stand = letzterStand;
                    }

                    if (stand?.Vorschlaege is not { Count: > 0 } vorschlaege)
                    {
                        Console.WriteLine("Wahl: noch keine Vorschläge");
                        return;
                    }

                    Vorschlaege("Vorschläge zur Wahl", vorschlaege, stand.Massstab);
                    Vorschlag v = vorschlaege[Math.Clamp(nummer, 1, vorschlaege.Count) - 1];
                    Console.WriteLine($"Wahl: {v.Name}, Hebel {(hebel is int h ? Massstab.Gruppen[h].Hebel : "keiner")}");
                    steuerung.Waehlen(v.Plan, hebel);
                },
                null,
                int.Parse(t[0], CultureInfo.InvariantCulture) * 1000,
                Timeout.Infinite);
        })];
        string? planDatei = werte.GetValueOrDefault("--plan");
        List<Timer> zwischen = planDatei is null ? [] : [.. werte.GetValueOrDefault("--zwischen", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(text =>
        {
            int nach = int.Parse(text, CultureInfo.InvariantCulture);
            return new Timer(
                _ =>
                {
                    Schnellstand? stand;
                    lock (lockStand)
                    {
                        stand = letzterStand;
                    }

                    if (stand?.Bester is { } b)
                    {
                        string ziel = Zwischendatei(planDatei, nach);
                        GespeicherterPlanDatei.Speichern(ziel, b.Plan.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
                        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Zwischenstand nach {nach} s gespeichert: {Kurz(b.Guete, stand.Massstab)}"));
                    }
                },
                null,
                nach * 1000,
                Timeout.Infinite);
        })];
        if (mitbestimmen)
        {
            Console.WriteLine("Mitbestimmen: Enter = Beweisstand und Vorschläge zeigen, „2“ = Vorschlag 2 wählen, „2a“ = mit Hebel a, „x“ = beenden.");
            Console.WriteLine();
        }

        Task<Schnellergebnis> lauf = Task.Run(() => Schnellplaner.Planen(
            staffel,
            optionen,
            HighsLoeser.Verfuegbar ? samen => new SchrankeBefehl.Highsschranke(new Highsvorgabe(samen)) : null,
            e,
            steuerung,
            stand =>
            {
                Schnellstand? vorher;
                lock (lockStand)
                {
                    vorher = letzterStand;
                    letzterStand = stand;
                }

                if (mitbestimmen)
                {
                    Knapp(stand, vorher);
                    return;
                }

                string zeile = string.Create(CultureInfo.InvariantCulture, $"[{stand.Sekunden,6:0.0} s] {stand.Text}");
                if (zeile != zuletzt)
                {
                    Console.WriteLine(zeile);
                    zuletzt = zeile;
                }
            },
            abbruch.Token));
        if (mitbestimmen)
        {
            Dialog(lauf, steuerung, abbruch, () =>
            {
                lock (lockStand)
                {
                    return letzterStand;
                }
            });
        }

        Schnellergebnis ergebnis = await lauf;
        wahlen.ForEach(t => t.Dispose());
        zwischen.ForEach(t => t.Dispose());

        Massstab m = ergebnis.Massstab;
        Console.WriteLine();
        Console.WriteLine($"Erster gültiger Plan: {Sekunden(ergebnis.ErsterGueltigerPlan)}; Pflicht und Wünsche fertig (HiGHS): {Sekunden(ergebnis.AUndBFertig)}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Maßstab: Fokus {(m.Fokus is int fg ? Massstab.Gruppen[fg].Name : "keiner")}; Gewichte {string.Join(", ", Massstab.Gruppen.Select((g, i) => $"{g.Name} {m.Gewichte[i]}"))}"));
        Console.WriteLine();
        Console.WriteLine("Kandidaten (bester zuerst; Gruppen bezogen auf den Grundplan):");
        foreach (Schnellkandidat k in ergebnis.Kandidaten)
        {
            Planguete g = k.Bester(m).Guete;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {k.Name,-28} {Kurz(g, m)}   {k.Runden} Runden, {k.Sekunden:0} s, Erfolge {k.Erfolge:0.0}"));
        }

        Console.WriteLine();
        Console.WriteLine($"Grenze des Machbaren ({ergebnis.Grenze.Count} Pläne):");
        foreach (Grenzplan p in ergebnis.Grenze.OrderBy(p => m.Guete(p.Guete)))
        {
            Console.WriteLine("  " + Kurz(p.Guete, m));
        }

        Console.WriteLine();
        Tabelle(ergebnis);
        Wippe(ergebnis);
        Beweise(ergebnis);
        Vorschlaege($"Vorschläge (Stufe {ergebnis.Stufe})", ergebnis.Vorschlaege ?? [], m);
        if (werte.TryGetValue("--plan", out string? plan) && ergebnis.Plan.Count > 0)
        {
            GespeicherterPlanDatei.Speichern(plan, ergebnis.Plan.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
            Console.WriteLine($"Plan gespeichert: {plan}");
        }

        return 0;
    }

    /// <summary>Im Dialog nur das Wichtige: Phasen, Stufen, ein neuer bester Plan, neue Vorschläge.</summary>
    private static void Knapp(Schnellstand stand, Schnellstand? vorher)
    {
        string zeit = string.Create(CultureInfo.InvariantCulture, $"[{stand.Sekunden,6:0.0} s]");
        if (stand.Text.StartsWith("Phase", StringComparison.Ordinal) || stand.Text.StartsWith("Stufe", StringComparison.Ordinal)
            || stand.Text.StartsWith("Pflicht und Wünsche", StringComparison.Ordinal) || stand.Text.StartsWith("HiGHS: Pflicht", StringComparison.Ordinal))
        {
            Console.WriteLine($"{zeit} {stand.Text}");
        }

        if (stand.Bester is { } b && (vorher?.Bester is not { } a || stand.Massstab.IstBesser(b.Guete, a.Guete)))
        {
            Console.WriteLine($"{zeit} Bester Plan: {Kurz(b.Guete, stand.Massstab)}");
        }

        static string Signatur(Schnellstand? s) => string.Join("|", (s?.Vorschlaege ?? []).Select(v => v.Name));
        if (stand.Vorschlaege is { Count: > 0 } && Signatur(stand) != Signatur(vorher))
        {
            Console.WriteLine($"{zeit} Neue Vorschläge ({stand.Vorschlaege.Count}) – Enter zeigt sie.");
        }
    }

    /// <summary>
    /// Der Dialog des Mitbestimmens auf der Konsole (wie später die Karten der Oberfläche): Enter zeigt Beweisstand, Vorschläge
    /// und Hebel; „2“ wählt Vorschlag 2, „2a“ dazu Hebel a; „x“ beendet.
    /// </summary>
    private static void Dialog(Task lauf, Schnellsteuerung steuerung, CancellationTokenSource abbruch, Func<Schnellstand?> stand)
    {
        while (!lauf.IsCompleted)
        {
            string? eingabe = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (eingabe is null || eingabe == "x")
            {
                Console.WriteLine("Beenden …");
                abbruch.Cancel();
                return;
            }

            Schnellstand? s = stand();
            if (s?.Vorschlaege is not { Count: > 0 } vorschlaege)
            {
                Console.WriteLine("Noch keine Vorschläge – erst Pflicht und Wünsche, dann glätten (gut eine Minute).");
                continue;
            }

            if (eingabe.Length == 0)
            {
                Anzeigen(s, vorschlaege);
                continue;
            }

            int ziffern = eingabe.TakeWhile(char.IsDigit).Count();
            if (ziffern == 0 || !int.TryParse(eingabe[..ziffern], CultureInfo.InvariantCulture, out int nummer) || nummer < 1 || nummer > vorschlaege.Count)
            {
                Console.WriteLine($"Bitte eine Nummer von 1 bis {vorschlaege.Count}, optional mit Hebel (z. B. „1a“), Enter oder „x“.");
                continue;
            }

            string rest = eingabe[ziffern..].Trim();
            int? hebel = rest.Length == 1 && rest[0] - 'a' is int h && h >= 0 && h < Massstab.Gruppen.Count ? h : null;
            if (rest.Length > 0 && hebel is null)
            {
                Console.WriteLine($"Unbekannter Hebel „{rest}“ – Buchstaben a bis {(char)('a' + Massstab.Gruppen.Count - 1)}.");
                continue;
            }

            Vorschlag v = vorschlaege[nummer - 1];
            Console.WriteLine($"Eingabe {eingabe}: {v.Name}, Hebel {(hebel is int hh ? Massstab.Gruppen[hh].Hebel : "keiner (ausgewogen verfeinern)")} – Stufe {steuerung.Stufe + 1} beginnt.");
            steuerung.Waehlen(v.Plan, hebel);
        }
    }

    /// <summary>Beweisstand des besten Plans, die Vorschläge und die Hebel der offenen Gruppen.</summary>
    private static void Anzeigen(Schnellstand s, IReadOnlyList<Vorschlag> vorschlaege)
    {
        Console.WriteLine();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Stand nach {s.Sekunden:0} s, Stufe {s.Stufe}:"));
        foreach (IGrouping<Beweisstand, Kriteriumsstatus> gruppe in (s.Status ?? []).Where(k => k.Stand != Beweisstand.Optimal).GroupBy(k => k.Stand).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  {gruppe.Key,-15} {string.Join(", ", gruppe.Select(k => $"{Massstab.Kriteriumsname(k.Kriterium)} {k.Anzahl}"))}");
        }

        Vorschlaege("Vorschläge", vorschlaege, s.Massstab);
        Console.WriteLine();
        Console.WriteLine("Hebel:");
        Grenzplan empfehlung = vorschlaege[0].Plan;
        for (int g = 0; g < Massstab.Gruppen.Count; g++)
        {
            if (empfehlung.Gruppen[g] > 1e-9)
            {
                Console.WriteLine($"  {(char)('a' + g)}) {Massstab.Gruppen[g].Hebel}");
            }
        }

        Console.WriteLine("Auswahl: „1“ … oder „1a“ …, Enter = neu anzeigen, „x“ = beenden");
    }

    /// <summary>Eine Gruppe nach ihrem Namen (Anfang genügt, z. B. „heim“).</summary>
    private static int Gruppe(string name)
    {
        for (int g = 0; g < Massstab.Gruppen.Count; g++)
        {
            if (Massstab.Gruppen[g].Name.StartsWith(name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return g;
            }
        }

        throw new FormatException($"Unbekannte Gruppe: {name} (bekannt: {string.Join(", ", Massstab.Gruppen.Select(g => g.Name))})");
    }

    private static string Kurz(Planguete g, Massstab m) => string.Create(
        CultureInfo.InvariantCulture,
        $"Pflicht {g.Harte}·{g.A} · Wünsche {g.Wuensche} · Güte {m.Guete(g),5:0.00} │ {string.Join(" ", Enumerable.Range(0, Massstab.Gruppen.Count).Select(i => $"{Massstab.Gruppen[i].Name[..Math.Min(6, Massstab.Gruppen[i].Name.Length)]} {m.Gruppe(g, i):0.00}"))}");

    /// <summary>Grundplan (nach A und B) und bester Plan je Kriterium und Gruppe.</summary>
    private static void Tabelle(Schnellergebnis ergebnis)
    {
        if (ergebnis.Guete is not { } besterWert)
        {
            Console.WriteLine("Kein Plan gefunden.");
            return;
        }

        Massstab m = ergebnis.Massstab;
        Planguete grund = ergebnis.Grundplan?.Guete ?? besterWert;
        Console.WriteLine($"{string.Empty,-32} {"Nach A/B",10} {"Bester",10}");
        Console.WriteLine($"{"Harte Fehler",-32} {grund.Harte,10} {besterWert.Harte,10}");
        Console.WriteLine($"{"Pflicht (A)",-32} {grund.A,10} {besterWert.A,10}");
        Console.WriteLine($"{"Wünsche der Vereine",-32} {grund.Wuensche,10} {besterWert.Wuensche,10}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{"Güte (Maßstab)",-32} {m.Guete(grund),10:0.00} {m.Guete(besterWert),10:0.00}"));
        for (int g = 0; g < Massstab.Gruppen.Count; g++)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {Massstab.Gruppen[g].Name + " ×" + m.Gewichte[g].ToString(CultureInfo.InvariantCulture),-30} {m.Gruppe(grund, g),10:0.00} {m.Gruppe(besterWert, g),10:0.00}"));
            foreach (Kostenkriterium k in Massstab.Gruppen[g].Kriterien)
            {
                Console.WriteLine($"    {k,-28} {grund.Anzahl[(int)k],10} {besterWert.Anzahl[(int)k],10}");
            }
        }

        foreach (Kostenkriterium k in Planguete.Pflicht.Concat(Planguete.Wunschkriterien))
        {
            Console.WriteLine($"  {k,-30} {grund.Anzahl[(int)k],10} {besterWert.Anzahl[(int)k],10}");
        }
    }

    /// <summary>Die Wippe für den besten Plan: je Gruppe, was ihr Senken an der Grenze bei den anderen kostet.</summary>
    private static void Wippe(Schnellergebnis ergebnis)
    {
        if (ergebnis.Guete is not { } besterWert)
        {
            return;
        }

        Massstab m = ergebnis.Massstab;
        var front = new Paretofront();
        foreach (Grenzplan p in ergebnis.Grenze)
        {
            front.Aufnehmen(p.Plan, p.Guete, m);
        }

        double[] aktuell = [.. Enumerable.Range(0, Massstab.Gruppen.Count).Select(g => m.Gruppe(besterWert, g))];
        Console.WriteLine();
        Console.WriteLine("Wippe (bester Plan): Gruppe senken → was steigt (bekannte Pläne an der Grenze)");
        for (int g = 0; g < Massstab.Gruppen.Count; g++)
        {
            if (aktuell[g] <= 0)
            {
                continue;
            }

            string text = front.Senken(aktuell, g, m) is { } s
                ? string.Join(", ", s.Aenderung.Select((d, i) => (d, i)).Where(e => Math.Abs(e.d) > 0.005).Select(e => string.Create(CultureInfo.InvariantCulture, $"{Massstab.Gruppen[e.i].Name} {e.d:+0.00;-0.00}")))
                : "an der Grenze – kein bekannter Plan mit weniger";
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {Massstab.Gruppen[g].Name,-22} ({aktuell[g]:0.00}): {text}"));
        }
    }

    /// <summary>Was über die Kriterien des besten Plans bewiesen ist, und was offen bleibt (die Leiste der Oberfläche).</summary>
    private static void Beweise(Schnellergebnis ergebnis)
    {
        if (ergebnis.Status is not { } status)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Bewiesen und offen (bester Plan):");
        foreach (IGrouping<Beweisstand, Kriteriumsstatus> stand in status.GroupBy(s => s.Stand).OrderBy(g => g.Key))
        {
            string text = stand.Key == Beweisstand.Optimal
                ? string.Join(", ", stand.Select(s => Massstab.Kriteriumsname(s.Kriterium)))
                : string.Join(", ", stand.Select(s => $"{Massstab.Kriteriumsname(s.Kriterium)} {s.Anzahl}{(s.Untergrenze is int u && u > 0 ? $" (≥ {u})" : string.Empty)}"));
            Console.WriteLine($"  {stand.Key,-15} {text}");
        }
    }

    /// <summary>Die Vorschläge mit ihren Unterschieden zur Empfehlung.</summary>
    private static void Vorschlaege(string titel, IReadOnlyList<Vorschlag> vorschlaege, Massstab m)
    {
        Console.WriteLine();
        Console.WriteLine($"{titel}:");
        for (int i = 0; i < vorschlaege.Count; i++)
        {
            Vorschlag v = vorschlaege[i];
            Console.WriteLine($"  {i + 1}. {v.Name,-34} {Kurz(v.Plan.Guete, m)}");
            if (v.Unterschiede.Count > 0)
            {
                Console.WriteLine($"     {string.Join(", ", v.Unterschiede)}");
            }
        }

        if (vorschlaege.Count == 1)
        {
            Console.WriteLine("     (Die bekannten Alternativen unterscheiden sich kaum – der Plan liegt nahe am Optimum.)");
        }
    }

    private static string Sekunden(double? wert) => wert is double s ? string.Create(CultureInfo.InvariantCulture, $"{s:0.0} s") : "–";
}
