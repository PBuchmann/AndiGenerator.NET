// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen schranke</c>: untere Schranke der Stufe A mit HiGHS (MIGRATIONSPLAN O7, Schritt 1) – wie viele Verstöße jeder
/// Plan mindestens hat.
/// </summary>
internal static class SchrankeBefehl
{
    public const string Hilfe = """
        Aufruf: andigen schranke <click-TT-Exportdatei.xml> [Optionen]
          --optionen DATEI   Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --sekunden N       Zeitgrenze je Stufe (Standard 60)
          --stufen TEXT      Einteilung, z. B. "A:Hallenbelegung,ParalleleSpiele;B:Sperrtermine" (Standard: E14)
          --rundenplanung R  beide | halbrunde | vorrunde | rueckrunde (überschreibt die Optionsdatei)
          --plan DATEI       besten Plan des vereinfachten Modells als gemerkten Plan speichern (zum Prüfen mit "bewerten")
          --start DATEI      Startlösung (CSV oder gemerkter Plan), wie im Automodus der beste Plan der Generierung
        Eine .modifications-Datei neben der XML-Datei wird automatisch berücksichtigt.
        """;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static int Ausfuehren(IReadOnlyList<string> args)
    {
        string? datei = null;
        string? optionenDatei = null;
        string? planDatei = null;
        string? startDatei = null;
        Rundenplanung? rundenplanung = null;
        Stufeneinteilung? stufen = null;
        int sekunden = 60;
        var werte = new Queue<string>(args);
        while (werte.Count > 0)
        {
            string arg = werte.Dequeue();
            if (arg == "--optionen" && werte.Count > 0)
            {
                optionenDatei = werte.Dequeue();
            }
            else if (arg == "--plan" && werte.Count > 0)
            {
                planDatei = werte.Dequeue();
            }
            else if (arg == "--start" && werte.Count > 0)
            {
                startDatei = werte.Dequeue();
            }
            else if (arg == "--stufen" && werte.Count > 0 && Stufeneinteilung.Lesen(werte.Peek()) is Stufeneinteilung e)
            {
                stufen = e;
                werte.Dequeue();
            }
            else if (arg == "--rundenplanung" && werte.Count > 0 && Rundenwahl.Lesen(werte.Peek()) is Rundenplanung r)
            {
                rundenplanung = r;
                werte.Dequeue();
            }
            else if (arg == "--sekunden" && werte.Count > 0 && int.TryParse(werte.Peek(), CultureInfo.InvariantCulture, out int s) && s > 0)
            {
                sekunden = s;
                werte.Dequeue();
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal) || datei is not null)
            {
                Console.WriteLine(Hilfe);
                return 1;
            }
            else
            {
                datei = arg;
            }
        }

        if (datei is null)
        {
            Console.WriteLine(Hilfe);
            return 1;
        }

        if (!HighsLoeser.Verfuegbar)
        {
            Console.Error.WriteLine("HiGHS ist auf diesem Rechner nicht verfügbar.");
            return 2;
        }

        Staffel staffel = OptimierenBefehl.StaffelLaden(datei, leer: false);
        (Berechnungsoptionen optionen, string herkunft) = OptimierenBefehl.OptionenLaden(optionenDatei, staffel);
        if (rundenplanung is Rundenplanung runde)
        {
            optionen = optionen with { Rundenplanung = runde };
            herkunft += $", Rundenplanung {runde}";
        }

        Console.WriteLine($"Staffel:      {staffel.Name} ({staffel.Mannschaften.Count} Mannschaften)");
        Console.WriteLine($"Optionen:     {herkunft}");
        Console.WriteLine();

        List<Spiel>? start = null;
        if (startDatei is not null && (start = BewertenBefehl.PlanLaden(staffel, startDatei)) is null)
        {
            return 1;
        }

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, optionen, new Highsschranke(), TimeSpan.FromSeconds(sekunden), stufen, startplan: start is null ? null : () => start);
        Console.WriteLine(string.Create(Deutsch, $"Modell:       {bericht.Spalten:N0} Spalten, {bericht.Zeilen:N0} Zeilen"));
        Console.WriteLine();
        Console.WriteLine("Stufe  Kriterium                                            Schranke  allein  Modell  bewiesen     Zeit");
        foreach (Schrankenstufe s in new[] { bericht.SummeA, bericht.SummeB }.OfType<Schrankenstufe>().Concat(bericht.Stufen))
        {
            Console.WriteLine(string.Create(
                Deutsch,
                $"{s.Stufe,-6} {s.Name,-52} {s.Schranke,8} {s.Einzeln,7} {s.Gefunden?.ToString(Deutsch) ?? "-",7}  {(s.Bewiesen ? "ja" : "nein"),-8} {s.Sekunden,6:F1} s"));
        }

        foreach (string hinweis in bericht.Hinweise)
        {
            Console.WriteLine("Hinweis: " + hinweis);
        }

        // Varianten in B: je Kriterium mit Vorrang die echten Verstöße der B-Kriterien (und A zusammen).
        Stufeneinteilung einteilung = (stufen ?? Stufeneinteilung.Standard).Vervollstaendigt();
        foreach (Planalternative alternative in bericht.Alternativen ?? [])
        {
            Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = alternative.Plan }, optionen);
            Stufenwert wert = einteilung.Zaehlen(bewertung);
            string b = string.Join(", ", einteilung.B.Select(k => $"{k} {bewertung.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)k].Anzahl))}"));
            Console.WriteLine(string.Create(Deutsch, $"Variante: {(alternative.Kriterium is null ? alternative.Name : "Vorrang " + alternative.Name)}{(alternative.Bewiesen ? " (bewiesen)" : string.Empty)} – A {wert.A}, B {wert.B} ({b})"));
        }

        if (planDatei is not null && bericht.Plan.Count > 0)
        {
            GespeicherterPlanDatei.Speichern(planDatei, bericht.Plan.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
            Console.WriteLine($"Plan gespeichert: {planDatei}");
        }

        return 0;
    }

    /// <summary>Reicht HiGHS als <see cref="ISchrankenloeser"/> herein.</summary>
    internal sealed class Highsschranke : ISchrankenloeser
    {
        public Schrankenloesung Loesen(Ganzzahlmodell modell, TimeSpan zeitgrenze, double[]? start = null, Func<bool>? aufhoeren = null)
        {
            var problem = new Ganzzahlproblem(
                [.. modell.Kosten],
                [.. modell.SpalteOben],
                [.. modell.ZeileUnten],
                [.. modell.ZeileOben],
                [.. modell.ZeilenAnfang],
                [.. modell.EintragSpalten],
                [.. modell.EintragWerte]);
            (double[]? werte, double wert, double schranke, bool optimal, bool unloesbar) = HighsLoeser.LoesenMitSchranke(problem, zeitgrenze, start, aufhoeren);
            return new Schrankenloesung(werte, wert, schranke, optimal, unloesbar);
        }
    }
}
