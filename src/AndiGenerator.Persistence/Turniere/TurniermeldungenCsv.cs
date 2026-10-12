// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Persistence.Turniere;

/// <summary>
/// Liest die Meldungen einer Turnierrunde aus der CSV-Datei einer Online-Umfrage (z. B. Google Formulare): je Zeile eine
/// Meldung mit Mannschaft, je Sonntag einer Spalte „TT.MM.JJJJ“ (Antwort „möglicher Heimspieltag“ und/oder „möglicher
/// Auswärtsspieltag“ oder „können nicht“), Halle, „zwei Spiele parallel“ und Bemerkungen. Meldet eine Mannschaft
/// mehrfach, gilt die letzte Zeile. Falsch kodierte Umlaute („kÃ¶nnen“) werden repariert.
/// </summary>
public static class TurniermeldungenCsv
{
    /// <summary>Liest die Meldungen aus einer Datei.</summary>
    /// <param name="pfad">Pfad der CSV-Datei.</param>
    /// <returns>Die Meldungen, je Mannschaft eine, in der Reihenfolge der ersten Meldung.</returns>
    /// <exception cref="FormatException">Spalten für Mannschaft oder Termine fehlen.</exception>
    public static IReadOnlyList<Turniermeldung> Laden(string pfad) => Lesen(TextLesen(File.ReadAllBytes(pfad)));

    /// <summary>Liest die Meldungen aus dem Text einer CSV-Datei.</summary>
    /// <param name="text">Inhalt der Datei.</param>
    /// <returns>Die Meldungen, je Mannschaft eine, in der Reihenfolge der ersten Meldung.</returns>
    /// <exception cref="FormatException">Spalten für Mannschaft oder Termine fehlen.</exception>
    public static IReadOnlyList<Turniermeldung> Lesen(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<List<string>> zeilen = Zerlegen(Reparieren(text));
        if (zeilen.Count == 0)
        {
            throw new FormatException("Die Datei mit den Meldungen ist leer.");
        }

        List<string> kopf = zeilen[0];
        int mannschaft = kopf.FindIndex(k => k.Contains("Mannschaft", StringComparison.OrdinalIgnoreCase));
        int halle = kopf.FindIndex(k => k.Contains("Halle", StringComparison.OrdinalIgnoreCase) && !k.Contains("parallel", StringComparison.OrdinalIgnoreCase));
        int parallel = kopf.FindIndex(k => k.Contains("parallel", StringComparison.OrdinalIgnoreCase));
        int bemerkung = kopf.FindIndex(k => k.TrimStart().StartsWith("Bemerkung", StringComparison.OrdinalIgnoreCase));
        var tage = new List<(int Spalte, DateOnly Tag)>();
        for (int i = 0; i < kopf.Count; i++)
        {
            if (Datum(kopf[i]) is DateOnly tag)
            {
                tage.Add((i, tag));
            }
        }

        if (mannschaft < 0 || tage.Count == 0)
        {
            throw new FormatException("In der Kopfzeile fehlen die Spalte mit der Mannschaft oder die Spalten mit den Terminen (TT.MM.JJJJ).");
        }

        var meldungen = new Dictionary<string, Turniermeldung>(StringComparer.OrdinalIgnoreCase);
        var reihenfolge = new List<string>();
        foreach (List<string> zeile in zeilen.Skip(1))
        {
            string name = Feld(zeile, mannschaft);
            if (name.Length == 0)
            {
                continue;
            }

            var bereitschaft = new Dictionary<DateOnly, Turnierbereitschaft>();
            foreach ((int spalte, DateOnly tag) in tage)
            {
                bereitschaft[tag] = Bereitschaft(Feld(zeile, spalte));
            }

            if (!meldungen.ContainsKey(name))
            {
                reihenfolge.Add(name);
            }

            meldungen[name] = new Turniermeldung(
                name,
                bereitschaft,
                Feld(zeile, halle),
                Feld(zeile, parallel).StartsWith("Ja", StringComparison.OrdinalIgnoreCase),
                Feld(zeile, bemerkung));
        }

        return reihenfolge.Select(n => meldungen[n]).ToList();
    }

    /// <summary>Antwort einer Zelle: „Heimspieltag“ und „Auswärtsspieltag“ können beide vorkommen.</summary>
    /// <param name="zelle">Inhalt der Zelle.</param>
    /// <returns>Die Bereitschaft.</returns>
    public static Turnierbereitschaft Bereitschaft(string zelle)
    {
        ArgumentNullException.ThrowIfNull(zelle);
        Turnierbereitschaft b = Turnierbereitschaft.None;
        if (zelle.Contains("Heimspieltag", StringComparison.OrdinalIgnoreCase))
        {
            b |= Turnierbereitschaft.Heim;
        }

        if (zelle.Contains("Auswärtsspieltag", StringComparison.OrdinalIgnoreCase)
            || zelle.Contains("Auswaertsspieltag", StringComparison.OrdinalIgnoreCase))
        {
            b |= Turnierbereitschaft.Auswaerts;
        }

        return b;
    }

    /// <summary>UTF-8 mit oder ohne BOM; ohne gültiges UTF-8 Windows-1252.</summary>
    private static string TextLesen(byte[] inhalt)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(inhalt).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(inhalt);
        }
    }

    /// <summary>Repariert UTF-8, das einmal als Latin-1 gelesen wurde („kÃ¶nnen“ → „können“).</summary>
    private static string Reparieren(string text)
    {
        if (!text.Contains('Ã', StringComparison.Ordinal) || text.Any(c => c > 'ÿ'))
        {
            return text;
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(Encoding.Latin1.GetBytes(text));
        }
        catch (DecoderFallbackException)
        {
            return text;
        }
    }

    /// <summary>CSV nach RFC 4180: Komma als Trenner, Felder in Anführungszeichen dürfen Kommas und Zeilenumbrüche enthalten.</summary>
    private static List<List<string>> Zerlegen(string text)
    {
        var zeilen = new List<List<string>>();
        var zeile = new List<string>();
        var feld = new StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            i++;
            if (c == '"')
            {
                i = ZitatLesen(text, i, feld) + 1;
            }
            else if (c == ',')
            {
                zeile.Add(feld.ToString());
                feld.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i < text.Length && text[i] == '\n')
                {
                    i++;
                }

                zeile.Add(feld.ToString());
                feld.Clear();
                Abschliessen(zeilen, zeile);
                zeile = [];
            }
            else
            {
                feld.Append(c);
            }
        }

        zeile.Add(feld.ToString());
        Abschliessen(zeilen, zeile);
        return zeilen;
    }

    /// <summary>Liest ein Feld in Anführungszeichen ab <paramref name="start"/> (doppelte Anführungszeichen stehen für eines).</summary>
    /// <returns>Index des schließenden Anführungszeichens.</returns>
    private static int ZitatLesen(string text, int start, StringBuilder feld)
    {
        int i = start;
        while (i < text.Length)
        {
            if (text[i] != '"')
            {
                feld.Append(text[i]);
            }
            else if (i + 1 < text.Length && text[i + 1] == '"')
            {
                feld.Append('"');
                i++;
            }
            else
            {
                return i;
            }

            i++;
        }

        return i;
    }

    private static void Abschliessen(List<List<string>> zeilen, List<string> zeile)
    {
        if (zeile.Exists(f => f.Trim().Length > 0))
        {
            zeilen.Add(zeile);
        }
    }

    private static string Feld(List<string> zeile, int spalte) => spalte >= 0 && spalte < zeile.Count ? zeile[spalte].Trim() : string.Empty;

    /// <summary>Datum am Anfang einer Kopfzelle, z. B. „ 15.11.2026 (Volkstrauertag)“.</summary>
    private static DateOnly? Datum(string kopf)
    {
        string text = kopf.Trim();
        return text.Length >= 10
            && DateOnly.TryParseExact(text[..10], "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly tag)
            ? tag
            : null;
    }
}
