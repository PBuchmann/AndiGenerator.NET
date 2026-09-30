// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Application;

/// <summary>
/// Kurzschreibweise eines Wunschtermins in der Terminübersicht (Original <c>THomeDay.AsShortString</c>/<c>FromShortString</c>),
/// z. B. <c>18:00 A K1:14:00 Max:2 L:3</c> oder <c>FREI</c>.
/// </summary>
public static class Heimtagtext
{
    /// <summary>Erzeugt den Text eines Termins (Original <c>AsShortString</c>).</summary>
    /// <param name="h">Termin oder <c>null</c> (kein Termin).</param>
    /// <returns>Der Text; leer ohne Termin.</returns>
    public static string Text(Heimtag? h)
    {
        if (h is null)
        {
            return string.Empty;
        }

        if (h.Sperrtermin)
        {
            return "FREI";
        }

        var text = new System.Text.StringBuilder(Zeit(TimeOnly.FromDateTime(h.Datum)));
        if (h.Ausweichtermin)
        {
            text.Append(" A");
        }

        if (h.Koppeltermin)
        {
            text.Append(" K").Append(h.KoppelPrio.ToString(CultureInfo.InvariantCulture)).Append(':').Append(Zeit(h.KoppelZweitzeit));
        }
        else if (h.AuswaertsKoppelZweitzeit)
        {
            text.Append(" T2:").Append(Zeit(h.KoppelZweitzeit));
        }

        if (h.Doppeltermin)
        {
            text.Append(" D").Append(h.KoppelPrio.ToString(CultureInfo.InvariantCulture));
        }

        if (h.MaxParallel > 0)
        {
            text.Append(" Max:").Append(h.MaxParallel.ToString(CultureInfo.InvariantCulture));
        }

        if (h.Spiellokal.Length > 0)
        {
            text.Append(" L:").Append(LokalKodieren(h.Spiellokal));
        }

        return text.ToString();
    }

    /// <summary>
    /// Liest den Text eines Termins (Original <c>FromShortString</c>): Ohne Uhrzeit (und ohne <c>FREI</c>) gibt es keinen Termin.
    /// Ungültige Uhrzeiten wie <c>25:00</c> gelten als fehlend (im Original ein Absturz).
    /// </summary>
    /// <param name="tag">Tag des Termins.</param>
    /// <param name="text">Eingabe.</param>
    /// <returns>Der Termin oder <c>null</c>.</returns>
    public static Heimtag? Lesen(DateOnly tag, string? text)
    {
        var h = new Heimtag(tag.ToDateTime(TimeOnly.MinValue), false, false, false, 0, false, TimeOnly.MinValue, 0, false, string.Empty);
        bool gueltig = false;
        bool gesperrt = false;
        foreach (string s in Zerlegen((text ?? string.Empty).Trim()))
        {
            string gross = s.ToUpperInvariant();
            if (gross == "FREI")
            {
                gesperrt = true;
                gueltig = true;
                continue;
            }

            if (UhrzeitLesen(s) is TimeOnly zeit)
            {
                gueltig = true;
                h = h with { Datum = tag.ToDateTime(zeit) };
            }

            if (gross == "A")
            {
                h = h with { Ausweichtermin = true };
            }

            if (gross.StartsWith("L:", StringComparison.Ordinal))
            {
                h = h with { Spiellokal = LokalDekodieren(s[2..]) };
            }

            h = KoppelLesen(h, s, gross);

            if (gross.StartsWith("MAX:", StringComparison.Ordinal) && s.Length >= 5 && ZahlLesen(s[4..]) is int max and >= 0)
            {
                h = h with { MaxParallel = max };
            }
        }

        if (gesperrt)
        {
            return new Heimtag(tag.ToDateTime(TimeOnly.MinValue), true, false, false, 0, false, TimeOnly.MinValue, 0, false, string.Empty);
        }

        return gueltig ? h : null;
    }

    /// <summary>Original <c>FixCellStringValue</c>: Eingabe lesen und einheitlich wieder ausgeben (ungültig = leer).</summary>
    /// <param name="text">Eingabe.</param>
    /// <returns>Der bereinigte Text.</returns>
    public static string Bereinigen(string? text) => Text(Lesen(DateOnly.FromDateTime(DateTime.Today), text));

    /// <summary>Koppel (<c>K</c>), Doppelspieltag (<c>D</c>) und zweite Zeit für Auswärtskoppel (<c>T2</c>) in der Reihenfolge des Originals.</summary>
    private static Heimtag KoppelLesen(Heimtag h, string s, string gross)
    {
        if (!h.Doppeltermin && !h.AuswaertsKoppelZweitzeit && gross.StartsWith('K') && s.Length >= 8
            && UhrzeitLesen(Copy(s, 4, 5)) is TimeOnly koppelzeit && ZahlLesen(Copy(s, 2, 1)) is int prio and >= 0 and <= 2)
        {
            h = h with { Koppeltermin = true, KoppelPrio = prio, KoppelZweitzeit = koppelzeit };
        }

        if (!h.Koppeltermin && gross.StartsWith('D') && s.Length >= 2 && ZahlLesen(Copy(s, 2, 1)) is int doppelprio and >= 0 and <= 2)
        {
            h = h with { Doppeltermin = true, KoppelPrio = doppelprio };
        }

        if (!h.Koppeltermin && gross.StartsWith("T2", StringComparison.Ordinal) && s.Length >= 8 && UhrzeitLesen(Copy(s, 4, 5)) is TimeOnly zweitzeit)
        {
            h = h with { AuswaertsKoppelZweitzeit = true, KoppelZweitzeit = zweitzeit };
        }

        return h;
    }

    /// <summary>Original <c>THomeDay.TimeFromString</c>: <c>hh:mm</c> am Anfang des Textes.</summary>
    private static TimeOnly? UhrzeitLesen(string s)
    {
        if (Copy(s, 3, 1) != ":" || ZahlLesen(Copy(s, 1, 2)) is not int stunde || ZahlLesen(Copy(s, 4, 2)) is not int minute
            || stunde < 0 || minute < 0 || stunde > 23 || minute > 59)
        {
            return null;
        }

        return new TimeOnly(stunde, minute);
    }

    /// <summary>Delphi <c>StrToIntDef</c> ohne Vorgabe.</summary>
    private static int? ZahlLesen(string s) =>
        int.TryParse(s, NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int wert) ? wert : null;

    /// <summary>Delphi <c>Copy</c> (1-basiert, tolerant).</summary>
    private static string Copy(string s, int start, int laenge)
    {
        int von = start - 1;
        return von >= s.Length ? string.Empty : s.Substring(von, Math.Min(laenge, s.Length - von));
    }

    /// <summary>Original <c>SplitString</c>: Trennung an Leerzeichen außerhalb von Anführungszeichen.</summary>
    private static List<string> Zerlegen(string wert)
    {
        var teile = new List<string>();
        bool inZeichen = false;
        int i = 0;
        while (i < wert.Length)
        {
            if (wert[i] == '"')
            {
                inZeichen = !inZeichen;
            }

            if ((wert[i] == ' ' && !inZeichen) || i == wert.Length - 1)
            {
                string teil = wert[..(i + 1)].Trim();
                wert = wert[(i + 1)..].Trim();
                if (teil.Length > 0)
                {
                    teile.Add(teil);
                }

                i = 0;
            }
            else
            {
                i++;
            }
        }

        return teile;
    }

    private static string LokalKodieren(string lokal)
    {
        string ergebnis = lokal.Replace('"', '„');
        return ergebnis.Contains(' ', StringComparison.Ordinal) ? "\"" + ergebnis + "\"" : ergebnis;
    }

    private static string LokalDekodieren(string lokal)
    {
        string ergebnis = lokal.Length >= 2 && lokal.StartsWith('"') && lokal.EndsWith('"') ? lokal[1..^1] : lokal;
        return ergebnis.Replace('„', '"');
    }

    private static string Zeit(TimeOnly zeit) => zeit.ToString("HH:mm", CultureInfo.InvariantCulture);
}
