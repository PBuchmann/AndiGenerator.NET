// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Cli;

/// <summary>Argumente von <c>andigen bewerten</c>.</summary>
internal sealed class BewertenArgumente
{
    private readonly List<string> plaene = [];

    public string Datei { get; private set; } = string.Empty;

    public IReadOnlyList<string> Plaene => plaene;

    public string? Optionen { get; private set; }

    public Rundenplanung? Rundenplanung { get; private set; }

    public bool Details { get; private set; }

    public string? Ausgabe { get; private set; }

    /// <summary>Liest die Argumente nach dem Befehlswort; <c>null</c> bei Fehlern (Meldung in <paramref name="fehler"/>).</summary>
    public static BewertenArgumente? Lesen(IReadOnlyList<string> args, out string fehler)
    {
        var ergebnis = new BewertenArgumente();
        fehler = string.Empty;
        var werte = new Queue<string>(args);
        while (werte.Count > 0)
        {
            string arg = werte.Dequeue();
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                ergebnis.DateiHinzufuegen(arg);
            }
            else if (!ergebnis.Setzen(arg, werte, out fehler))
            {
                return null;
            }
        }

        if (ergebnis.plaene.Count == 0)
        {
            fehler = "Bitte die click-TT-Exportdatei und mindestens einen Plan angeben.";
            return null;
        }

        return ergebnis;
    }

    private void DateiHinzufuegen(string datei)
    {
        if (Datei.Length == 0)
        {
            Datei = datei;
        }
        else
        {
            plaene.Add(datei);
        }
    }

    private bool Setzen(string arg, Queue<string> werte, out string fehler)
    {
        fehler = string.Empty;
        if (arg == "--details")
        {
            Details = true;
            return true;
        }

        if (arg == "--hilfe")
        {
            return false;
        }

        if (werte.Count == 0)
        {
            fehler = $"{arg} erwartet einen Wert.";
            return false;
        }

        string wert = werte.Dequeue();
        switch (arg)
        {
            case "--optionen":
                Optionen = wert;
                return true;
            case "--ausgabe":
                Ausgabe = wert;
                return true;
            case "--rundenplanung":
                Rundenplanung = Rundenwahl.Lesen(wert);
                fehler = Rundenplanung is null ? $"--rundenplanung erwartet {Rundenwahl.Werte}." : string.Empty;
                return Rundenplanung is not null;
            default:
                fehler = $"Unbekannte Option: {arg}";
                return false;
        }
    }
}
