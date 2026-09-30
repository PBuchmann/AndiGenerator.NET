// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen bewerten</c>: bewertet einen oder mehrere fertige Pläne (auch aus anderen Programmen) mit dem Kostenmodell
/// des Originals und stellt sie nebeneinander – gleiche Staffel, gleiche Optionen für alle Pläne.
/// </summary>
internal static class BewertenBefehl
{
    public const string Hilfe = """
        Aufruf: andigen bewerten <click-TT-Exportdatei.xml> <Plan> [<Plan> ...] [Optionen]
          Plan: CSV im click-TT-Format (Spalten Datum, Uhrzeit, Heim-Mannschaft, Gast-Mannschaft;
                UTF-8 oder Windows-1252, auch aus anderen Programmen) oder gemerkter Plan des Originals (.xml)
          --optionen DATEI      Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --rundenplanung R     beide | halbrunde | vorrunde | rueckrunde (überschreibt die Optionsdatei)
          --details             zusätzlich die Meldungen je Mannschaft ausgeben
          --ausgabe DATEI       Bildschirmausgabe zusätzlich in diese Datei schreiben
        Eine .modifications-Datei neben der XML-Datei wird automatisch berücksichtigt.
        """;

    private const int Breite = 20;

    public static async Task<int> Ausfuehren(IReadOnlyList<string> args)
    {
        BewertenArgumente? a = BewertenArgumente.Lesen(args, out string fehler);
        if (a is null)
        {
            if (fehler.Length > 0)
            {
                await Console.Error.WriteLineAsync(fehler);
            }

            Console.WriteLine(Hilfe);
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
            return Bewerten(a);
        }
        finally
        {
            Console.SetOut(konsole);
        }
    }

    private static int Bewerten(BewertenArgumente a)
    {
        Staffel staffel = OptimierenBefehl.StaffelLaden(a.Datei, leer: true);
        (Berechnungsoptionen optionen, string herkunft) = OptimierenBefehl.OptionenLaden(a.Optionen, staffel);
        if (a.Rundenplanung is Rundenplanung r)
        {
            optionen = optionen with { Rundenplanung = r };
        }

        Console.WriteLine($"Staffel:      {staffel.Name} ({staffel.Mannschaften.Count} Mannschaften)");
        Console.WriteLine($"Optionen:     {herkunft}, Rundenplanung {optionen.Rundenplanung}");
        Console.WriteLine();

        var ergebnisse = new List<(string Name, IReadOnlyList<Spiel> Spiele, Planbewertung Bewertung)>();
        bool fehlerhaft = false;
        foreach (string plan in a.Plaene)
        {
            IReadOnlyList<GespeichertesSpiel> gelesen = string.Equals(Path.GetExtension(plan), ".csv", StringComparison.OrdinalIgnoreCase)
                ? PlanCsvLeser.Laden(plan)
                : GespeicherterPlanDatei.Laden(plan);
            IReadOnlyList<Spiel>? spiele = Zuordnen(staffel, gelesen, plan);
            if (spiele is null)
            {
                fehlerhaft = true;
                continue;
            }

            Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = spiele }, optionen);
            ergebnisse.Add((Path.GetFileNameWithoutExtension(plan), spiele, bewertung));
        }

        if (fehlerhaft || ergebnisse.Count == 0)
        {
            return 2;
        }

        Tabelle(ergebnisse);
        if (a.Details)
        {
            Details(ergebnisse);
        }

        return 0;
    }

    /// <summary>Ordnet die Mannschaftsnamen des Plans denen der Staffel zu (exakt, sonst ohne Leer- und Sonderzeichen).</summary>
    private static List<Spiel>? Zuordnen(Staffel staffel, IReadOnlyList<GespeichertesSpiel> gelesen, string plan)
    {
        List<string> namen = staffel.Mannschaften.Select(m => m.Name).ToList();
        var unbekannt = new SortedSet<string>(StringComparer.Ordinal);
        string Finden(string name)
        {
            string? treffer = namen.Find(n => n == name) ?? namen.Find(n => Vergleichsform(n) == Vergleichsform(name));
            if (treffer is null)
            {
                unbekannt.Add(name);
            }

            return treffer ?? name;
        }

        List<Spiel> spiele = gelesen.Select(s => new Spiel(s.Zeitpunkt, Finden(s.Heim), Finden(s.Gast), string.Empty)).ToList();
        if (unbekannt.Count == 0)
        {
            return spiele;
        }

        Console.Error.WriteLine($"{plan}: unbekannte Mannschaften: {string.Join(", ", unbekannt)}");
        return null;
    }

    private static string Vergleichsform(string name) => string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static void Tabelle(List<(string Name, IReadOnlyList<Spiel> Spiele, Planbewertung Bewertung)> ergebnisse)
    {
        Zeile("Plan", ergebnisse.Select(e => Kuerzen(e.Name)));
        Zeile("Spiele (terminiert)", ergebnisse.Select(e => $"{e.Spiele.Count} ({e.Spiele.Count(s => s.Zeitpunkt is not null)})"));
        Zeile("Gesamtkosten", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.Gesamtkosten)));
        Console.WriteLine();
        Console.WriteLine("Plan-Kostenarten:");
        Zeile("  nicht terminiert", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.NichtTerminiert)));
        Zeile("  ungültige Spiele", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.UngueltigeSpiele)));
        Zeile("  an spielfreien Tagen", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.SpieleAnSpielfreienTagen)));
        Zeile("  Überlappung Spieltage", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.UeberlappungSpieltage)));
        Zeile("  Länge Spieltage", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.LaengeSpieltage)));
        Zeile("  Überlappung letzter Sp.", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.UeberlappungLetzterSpieltag)));
        Zeile("  Länge letzter Spieltag", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.LaengeLetzterSpieltag)));
        Zeile("  Vereinsint. am Anfang", ergebnisse.Select(e => Kostenanzeige.Kurz(e.Bewertung.VereinsinterneSpieleAmAnfang)));
        Console.WriteLine();
        Console.WriteLine("Mannschafts-Kostenarten: Verstöße über alle Mannschaften (Kosten)");
        IEnumerable<MannschaftsKostenart> arten = ergebnisse.SelectMany(e => e.Bewertung.SichtbareKostenarten).Distinct().Order();
        foreach (MannschaftsKostenart art in arten)
        {
            Zeile("  " + Referenzbewertung.Kostenartnamen[(int)art], ergebnisse.Select(e => Kostenart(e.Bewertung, art)));
        }
    }

    private static string Kostenart(Planbewertung bewertung, MannschaftsKostenart art)
    {
        int anzahl = bewertung.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)art].Anzahl));
        double kosten = bewertung.Mannschaften.Sum(m => m.JeKostenart[(int)art].Kosten);
        return string.Create(CultureInfo.InvariantCulture, $"{anzahl} ({Kostenanzeige.Kurz(kosten)})");
    }

    private static void Details(List<(string Name, IReadOnlyList<Spiel> Spiele, Planbewertung Bewertung)> ergebnisse)
    {
        foreach ((string name, _, Planbewertung bewertung) in ergebnisse)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {name}");
            foreach (Mannschaftsbewertung mannschaft in bewertung.Mannschaften.Where(m => m.Meldungen.Count > 0))
            {
                Console.WriteLine($"  {mannschaft.Name} ({Kostenanzeige.Kurz(mannschaft.Gesamt)})");
                foreach (string meldung in mannschaft.Meldungen)
                {
                    Console.WriteLine($"    {meldung}");
                }
            }
        }
    }

    private static void Zeile(string beschriftung, IEnumerable<string> werte) =>
        Console.WriteLine($"{beschriftung,-28}" + string.Concat(werte.Select(w => $"{w,Breite} ")));

    private static string Kuerzen(string name) => name.Length <= Breite ? name : name[..(Breite - 1)] + "…";
}
