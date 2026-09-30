// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Cli;

/// <summary>Argumente von <c>andigen optimieren</c>.</summary>
internal sealed class OptimierenArgumente
{
    public const string Hilfe = """
        Aufruf: andigen optimieren <click-TT-Exportdatei.xml> [Optionen]
          --sekunden N      Laufzeit in Sekunden (Standard 120)
          --durchlaeufe N   stattdessen bis N Durchläufe insgesamt; mit --kerne 1 und --startwert reproduzierbar
          --kerne N         Rechen-Threads (Standard: Prozessorkerne - 1)
          --startwert N     Startwert der Zufallsfolgen
          --optionen DATEI  Optionsdatei (.options); "auto" = Staffelordner des Originals; Standard: Standardoptionen
          --rundenplanung R beide | halbrunde | vorrunde | rueckrunde (überschreibt die Optionsdatei)
          --leer            mit leerem Plan beginnen (sonst mit dem bestehenden Spielplan der Datei)
          --ohne-spezial    ohne Spezial-Insel
          --intervall N     Statuszeile alle N Sekunden (Standard 10)
          --protokoll DATEI Verlauf als CSV (Sekunden;Durchläufe;Pläne/s;Kosten;Verbesserungen;Neustarts;Spezial)
          --plan DATEI      besten Plan als gemerkten Plan (Format des Originals) speichern
          --ausgabe DATEI   Bildschirmausgabe zusätzlich in diese Datei schreiben
        Eine .modifications-Datei neben der XML-Datei wird automatisch berücksichtigt. Abbrechen mit Strg+C.
        """;

    public string Datei { get; private set; } = string.Empty;

    public int Sekunden { get; private set; } = 120;

    public long? Durchlaeufe { get; private set; }

    public int? Kerne { get; private set; }

    public int? Startwert { get; private set; }

    public string? Optionen { get; private set; }

    public Rundenplanung? Rundenplanung { get; private set; }

    public bool Leer { get; private set; }

    public bool OhneSpezial { get; private set; }

    public int Intervall { get; private set; } = 10;

    public string? Protokoll { get; private set; }

    public string? Plan { get; private set; }

    public string? Ausgabe { get; private set; }

    /// <summary>Liest die Argumente nach dem Befehlswort; <c>null</c> bei Fehlern (Meldung in <paramref name="fehler"/>).</summary>
    public static OptimierenArgumente? Lesen(IReadOnlyList<string> args, out string fehler)
    {
        var ergebnis = new OptimierenArgumente();
        fehler = string.Empty;
        var werte = new Queue<string>(args);
        while (werte.Count > 0)
        {
            string arg = werte.Dequeue();
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                ergebnis.Datei = arg;
                continue;
            }

            if (!ergebnis.Setzen(arg, werte, out fehler))
            {
                return null;
            }
        }

        if (ergebnis.Datei.Length == 0)
        {
            fehler = "Keine click-TT-Exportdatei angegeben.";
            return null;
        }

        return ergebnis;
    }

    private static bool Zahl(Queue<string> werte, string name, out long zahl, out string fehler)
    {
        fehler = string.Empty;
        zahl = 0;
        if (werte.Count == 0 || !long.TryParse(werte.Dequeue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out zahl) || zahl < 0)
        {
            fehler = $"{name} erwartet eine nicht negative Zahl.";
            return false;
        }

        return true;
    }

    private static bool Text(Queue<string> werte, string name, out string text, out string fehler)
    {
        fehler = werte.Count == 0 ? $"{name} erwartet einen Wert." : string.Empty;
        text = werte.Count == 0 ? string.Empty : werte.Dequeue();
        return fehler.Length == 0;
    }

    private bool Setzen(string arg, Queue<string> werte, out string fehler)
    {
        fehler = string.Empty;
        long zahl;
        string text;
        switch (arg)
        {
            case "--hilfe":
                return false;
            case "--leer":
                Leer = true;
                return true;
            case "--ohne-spezial":
                OhneSpezial = true;
                return true;
            case "--sekunden" when Zahl(werte, arg, out zahl, out fehler):
                Sekunden = (int)Math.Min(zahl, int.MaxValue);
                return true;
            case "--durchlaeufe" when Zahl(werte, arg, out zahl, out fehler):
                Durchlaeufe = zahl;
                return true;
            case "--kerne" when Zahl(werte, arg, out zahl, out fehler):
                Kerne = (int)Math.Clamp(zahl, 1, 1024);
                return true;
            case "--startwert" when Zahl(werte, arg, out zahl, out fehler):
                Startwert = (int)Math.Min(zahl, int.MaxValue);
                return true;
            case "--intervall" when Zahl(werte, arg, out zahl, out fehler):
                Intervall = (int)Math.Clamp(zahl, 1, 3600);
                return true;
            case "--optionen" when Text(werte, arg, out text, out fehler):
                Optionen = text;
                return true;
            case "--rundenplanung" when Text(werte, arg, out text, out fehler):
                Rundenplanung = Rundenwahl.Lesen(text);
                fehler = Rundenplanung is null ? $"--rundenplanung erwartet {Rundenwahl.Werte}." : string.Empty;
                return Rundenplanung is not null;
            case "--protokoll" when Text(werte, arg, out text, out fehler):
                Protokoll = text;
                return true;
            case "--plan" when Text(werte, arg, out text, out fehler):
                Plan = text;
                return true;
            case "--ausgabe" when Text(werte, arg, out text, out fehler):
                Ausgabe = text;
                return true;
            default:
                if (fehler.Length == 0)
                {
                    fehler = $"Unbekannte Option: {arg}";
                }

                return false;
        }
    }
}
