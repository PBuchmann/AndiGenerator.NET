// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Aufstellungen;

/// <summary>
/// Liest den click-TT-Export „Mannschaftsmeldung“ bzw. „Mannschaftsaufstellungen“ einer Staffel (CSV mit Semikolon,
/// Windows-1252): je Spieler eine Zeile mit Mannschaft der Staffel, Mannschaftsnummer und Rang des Spielers, TTR-Wert und
/// Status. Die Datei enthält alle Mannschaften der Vereine; gewertet werden nur die Spieler der Mannschaft, die in der
/// Staffel spielt (bei „TTC Wirges II“ die der 2. Mannschaft), ohne Reservespieler (Status „RES“). Namen und
/// Geburtsdaten werden nicht gelesen.
/// </summary>
public static class MannschaftsaufstellungenCsv
{
    private static readonly Dictionary<char, int> Roemisch = new() { ['I'] = 1, ['V'] = 5, ['X'] = 10 };

    /// <summary>Liest die Aufstellungen aus einer Datei (UTF-8 oder Windows-1252).</summary>
    /// <param name="pfad">Pfad der CSV-Datei.</param>
    /// <returns>Je Mannschaft der Staffel eine Aufstellung, in der Reihenfolge der Datei.</returns>
    /// <exception cref="FormatException">Die benötigten Spalten fehlen.</exception>
    public static IReadOnlyList<Mannschaftsaufstellung> Laden(string pfad)
    {
        byte[] inhalt = File.ReadAllBytes(pfad);
        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(inhalt);
        }
        catch (DecoderFallbackException)
        {
            text = DelphiKompatibel.Windows1252.GetString(inhalt);
        }

        return Lesen(text.TrimStart('﻿'));
    }

    /// <summary>Liest die Aufstellungen aus dem Text einer CSV-Datei.</summary>
    /// <param name="text">Inhalt der Datei.</param>
    /// <returns>Je Mannschaft der Staffel eine Aufstellung, in der Reihenfolge der Datei.</returns>
    /// <exception cref="FormatException">Die benötigten Spalten fehlen.</exception>
    public static IReadOnlyList<Mannschaftsaufstellung> Lesen(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string[]> zeilen = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(z => z.Split(';').Select(f => f.Trim().Trim('"')).ToArray())
            .ToList();
        if (zeilen.Count == 0)
        {
            throw new FormatException("Die Datei mit den Mannschaftsaufstellungen ist leer.");
        }

        string[] kopf = zeilen[0];
        int mannschaft = Spalte(kopf, "MannschaftName");
        int nummer = Spalte(kopf, "SpielerMannschaftNr");
        int rang = Spalte(kopf, "SpielerRang");
        int ttr = Spalte(kopf, "TTR");
        int status = Array.IndexOf(kopf, "SpielerStatus");
        if (Math.Min(Math.Min(mannschaft, nummer), Math.Min(rang, ttr)) < 0)
        {
            throw new FormatException("Die Datei ist keine Mannschaftsmeldung aus click-TT (es fehlen die Spalten MannschaftName, SpielerMannschaftNr, SpielerRang oder TTR).");
        }

        var spieler = new Dictionary<string, List<(int Rang, int Ttr)>>(StringComparer.Ordinal);
        foreach (string[] z in zeilen.Skip(1).Where(z => z.Length > Math.Max(Math.Max(mannschaft, nummer), Math.Max(rang, ttr))))
        {
            string name = z[mannschaft];
            if (!spieler.TryGetValue(name, out List<(int Rang, int Ttr)>? liste))
            {
                liste = [];
                spieler.Add(name, liste);
            }

            bool reserve = status >= 0 && status < z.Length && z[status].Contains("RES", StringComparison.Ordinal);
            if (!reserve && Zahl(z[nummer]) == Nummer(name) && Zahl(z[ttr]) is int wert && wert > 0)
            {
                liste.Add((Zahl(z[rang]) ?? int.MaxValue, wert));
            }
        }

        return spieler.Select(e => new Mannschaftsaufstellung(e.Key, e.Value.OrderBy(s => s.Rang).Select(s => s.Ttr).ToList())).ToList();
    }

    /// <summary>Mannschaftsnummer aus dem Namen: römische Zahl am Ende („TTC Wirges II“ → 2), sonst 1.</summary>
    /// <param name="name">Name der Mannschaft.</param>
    /// <returns>Die Nummer.</returns>
    public static int Nummer(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string[] woerter = name.Trim().Split(' ');
        string letztes = woerter[^1];
        if (letztes.Length == 0 || !letztes.All(Roemisch.ContainsKey))
        {
            return 1;
        }

        int summe = 0;
        for (int i = 0; i < letztes.Length; i++)
        {
            int wert = Roemisch[letztes[i]];
            summe += i + 1 < letztes.Length && Roemisch[letztes[i + 1]] > wert ? -wert : wert;
        }

        return summe;
    }

    private static int Spalte(string[] kopf, string name) => Array.IndexOf(kopf, name);

    private static int? Zahl(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wert) ? wert : null;
}
