// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Cli;

/// <summary>
/// Liest einen Spielplan im CSV-Format von click-TT (auch von anderen Programmen erzeugt): Spalten werden über die
/// Kopfzeile gefunden (<c>Datum</c>, <c>Uhrzeit</c>, <c>Heim-Mannschaft</c>, <c>Gast-Mannschaft</c>), Codierung UTF-8 oder Windows-1252.
/// </summary>
internal static class PlanCsvLeser
{
    private static readonly string[] Formate = ["dd.MM.yyyy HH:mm", "d.M.yyyy H:mm", "dd.MM.yyyy H:mm", "dd.MM.yy HH:mm"];

    public static IReadOnlyList<GespeichertesSpiel> Laden(string pfad)
    {
        byte[] bytes = File.ReadAllBytes(pfad);
        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = DelphiKompatibel.Windows1252.GetString(bytes);
        }

        string[] zeilen = text.TrimStart('﻿').ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (zeilen.Length == 0)
        {
            throw new FormatException($"{pfad}: Die Datei ist leer.");
        }

        string[] kopf = zeilen[0].Split(';');
        int datum = Spalte(kopf, "Datum", pfad);
        int uhrzeit = Spalte(kopf, "Uhrzeit", pfad);
        int heim = Spalte(kopf, "Heim-Mannschaft", pfad);
        int gast = Spalte(kopf, "Gast-Mannschaft", pfad);
        int mindestens = Math.Max(Math.Max(datum, uhrzeit), Math.Max(heim, gast)) + 1;

        var spiele = new List<GespeichertesSpiel>();
        for (int i = 1; i < zeilen.Length; i++)
        {
            string[] felder = zeilen[i].Split(';');
            if (felder.Length < mindestens)
            {
                throw new FormatException($"{pfad}, Zeile {i + 1}: zu wenige Spalten.");
            }

            spiele.Add(new GespeichertesSpiel(Zeitpunkt(felder[datum], felder[uhrzeit], pfad, i + 1), felder[heim].Trim(), felder[gast].Trim()));
        }

        return spiele;
    }

    private static DateTime? Zeitpunkt(string datum, string uhrzeit, string pfad, int zeile)
    {
        if (datum.Trim().Length == 0)
        {
            return null;
        }

        string zeit = uhrzeit.Trim().Length == 0 ? "00:00" : uhrzeit.Trim();
        return DateTime.TryParseExact($"{datum.Trim()} {zeit}", Formate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime wert)
            ? wert
            : throw new FormatException($"{pfad}, Zeile {zeile}: Termin „{datum} {uhrzeit}“ ist ungültig.");
    }

    private static int Spalte(string[] kopf, string name, string pfad)
    {
        int index = Array.FindIndex(kopf, k => string.Equals(k.Trim(), name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new FormatException($"{pfad}: Spalte „{name}“ fehlt in der Kopfzeile.");
    }
}
