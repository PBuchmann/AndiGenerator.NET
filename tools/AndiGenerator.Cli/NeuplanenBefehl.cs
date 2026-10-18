// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// <c>andigen neuplanen</c>: Teilneuplanung mit HiGHS (MIGRATIONSPLAN O7 Schritt 3) – Ausschnitte eines Plans neu planen,
/// A (und B) nicht schlechter, C besser.
/// </summary>
internal static class NeuplanenBefehl
{
    public const string Hilfe = """
        Aufruf: andigen neuplanen <click-TT-Exportdatei.xml> --start PLAN [Optionen]
          --start DATEI      Ausgangsplan (gemerkter Plan oder CSV)
          --optionen DATEI   Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --sekunden N       Zeit insgesamt (Standard 60)
          --schritt N        Zeitgrenze je Ausschnitt in Sekunden (Standard 5)
          --mannschaften N   Mannschaften je Ausschnitt mindestens (Standard 2; größere nach Erfolg je Sekunde)
          --ohne-wechsel     Wechsel H/A nicht ins Modell
          --ohne-abstand     Abstand H/A nicht ins Modell
          --ohne-setzliste   Setzliste nicht ins Modell (sonst genähert, wenn es eine gibt)
          --ohne-spieltage   zu lange und überlappende Spieltage nicht ins Modell
          --setzliste-genau  Setzliste genau nach der Reihenfolge der Spiele (sonst genähert; macht HiGHS langsamer)
          --abstand-genau    Abstand H/A genau nach der Reihenfolge der Spiele (sonst genähert; macht HiGHS viel langsamer)
          --rundenplanung R  beide | halbrunde | vorrunde | rueckrunde (überschreibt die Optionsdatei)
          --stufen TEXT      Einteilung, z. B. "A:Hallenbelegung,ParalleleSpiele;B:Sperrtermine" (Standard: E14)
          --startwert N      Zufall für die Wahl der Ausschnitte (Standard 1)
          --ausgabe DATEI    besten Plan als gemerkten Plan speichern
          --nur-zufall       nur zufällige Mannschaften als Ausschnitt (wie früher; sonst auch gezielt und Zeitfenster)
        """;

    public static int Ausfuehren(IReadOnlyList<string> args)
    {
        string? datei = null;
        string? optionenDatei = null;
        string? startDatei = null;
        string? ausgabe = null;
        Stufeneinteilung? stufen = null;
        Rundenplanung? rundenplanung = null;
        var e = new Neuplanungseinstellungen(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5));
        var werte = new Queue<string>(args);
        while (werte.Count > 0)
        {
            string arg = werte.Dequeue();
            int zahl = 0;
            bool mitZahl = werte.Count > 0 && int.TryParse(werte.Peek(), CultureInfo.InvariantCulture, out zahl) && zahl > 0;
            switch (arg)
            {
                case "--optionen" when werte.Count > 0:
                    optionenDatei = werte.Dequeue();
                    break;
                case "--start" when werte.Count > 0:
                    startDatei = werte.Dequeue();
                    break;
                case "--ausgabe" when werte.Count > 0:
                    ausgabe = werte.Dequeue();
                    break;
                case "--rundenplanung" when werte.Count > 0 && Rundenwahl.Lesen(werte.Peek()) is { } runde:
                    rundenplanung = runde;
                    werte.Dequeue();
                    break;
                case "--stufen" when werte.Count > 0 && Stufeneinteilung.Lesen(werte.Peek()) is { } einteilung:
                    stufen = einteilung;
                    werte.Dequeue();
                    break;
                case "--sekunden" when mitZahl:
                    e = e with { Gesamt = TimeSpan.FromSeconds(zahl) };
                    werte.Dequeue();
                    break;
                case "--schritt" when mitZahl:
                    e = e with { JeSchritt = TimeSpan.FromSeconds(zahl) };
                    werte.Dequeue();
                    break;
                case "--mannschaften" when mitZahl:
                    e = e with { Mannschaften = zahl };
                    werte.Dequeue();
                    break;
                case "--startwert" when mitZahl:
                    e = e with { Startwert = zahl };
                    werte.Dequeue();
                    break;
                case "--nur-zufall":
                    e = e with { Gezielt = false };
                    break;
                case "--ohne-wechsel":
                    e = e with { MitWechsel = false };
                    break;
                case "--ohne-abstand":
                    e = e with { MitAbstand = false };
                    break;
                case "--ohne-setzliste":
                    e = e with { MitSetzliste = false };
                    break;
                case "--ohne-spieltage":
                    e = e with { MitSpieltagen = false };
                    break;
                case "--setzliste-genau":
                    e = e with { SetzlisteGenau = true };
                    break;
                case "--abstand-genau":
                    e = e with { AbstandGenau = true };
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal) || datei is not null)
                    {
                        Console.WriteLine(Hilfe);
                        return 1;
                    }

                    datei = arg;
                    break;
            }
        }

        if (datei is null || startDatei is null)
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
        if (rundenplanung is Rundenplanung r)
        {
            optionen = optionen with { Rundenplanung = r };
            herkunft += $", Rundenplanung {r}";
        }

        if (BewertenBefehl.PlanLaden(staffel, startDatei) is not { } start)
        {
            return 2;
        }

        Console.WriteLine($"Staffel:      {staffel.Name} ({staffel.Mannschaften.Count} Mannschaften)");
        Console.WriteLine($"Optionen:     {herkunft}");
        Console.WriteLine($"Ausgangsplan: {startDatei}");
        Console.WriteLine();
        Stufeneinteilung gewaehlt = stufen ?? Stufeneinteilung.Standard;
        e = e with { Verstoesse = Ligaschranke.VerstoesseInC(staffel, optionen, gewaehlt) };
        Neuplanungsbericht bericht = Ligaschranke.Neuplanen(staffel, start, optionen, new SchrankeBefehl.Highsschranke(), gewaehlt, e, Console.WriteLine);
        Console.WriteLine();
        Console.WriteLine($"Vorher:  {bericht.Vorher}");
        Console.WriteLine($"Nachher: {bericht.Nachher} ({bericht.Angenommen} von {bericht.Schritte} Ausschnitten angenommen)");
        if (ausgabe is not null)
        {
            GespeicherterPlanDatei.Speichern(ausgabe, bericht.Plan.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)));
            Console.WriteLine($"Plan gespeichert: {ausgabe}");
        }

        return 0;
    }
}
