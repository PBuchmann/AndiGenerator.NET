// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;

namespace AndiGenerator.Persistence.Gemeinsam;

/// <summary>
/// Nachbildung von Delphi-Hilfsfunktionen, deren genaues Verhalten für die Dateikompatibilität zählt.
/// </summary>
public static class DelphiKompatibel
{
    /// <summary>Delphi-Nullpunkt von <c>TDateTime</c> (0.0 = 30.12.1899 00:00), im Original „kein Termin“.</summary>
    public static readonly DateTime DelphiNull = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Holt die Codierung Windows-1252; so schreibt Delphi (<c>TStringList.SaveToFile</c> ohne Encoding) die CSV-Datei.</summary>
    public static Encoding Windows1252
    {
        get
        {
            CodepagesRegistrieren();
            return Encoding.GetEncoding(1252);
        }
    }

    /// <summary>
    /// Stellt die Codepages Windows-1252 und iso-8859-15 bereit (nicht Teil der .NET-Grundausstattung).
    /// <c>Encoding.RegisterProvider</c> ist threadsicher und übergeht einen bereits registrierten Provider; deshalb wird bei
    /// jedem Aufruf registriert. (Ein vorher gesetztes Merkerflag ließ parallele Aufrufer vor dem Abschluss der Registrierung weiterlaufen.)
    /// </summary>
    public static void CodepagesRegistrieren() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// Delphi <c>Trim</c>: entfernt am Anfang und Ende alle Zeichen &lt;= ' ' (Leerzeichen und Steuerzeichen).
    /// Das Original trimmt beim Import jeden Wert (<c>TDataObject.SetAsString</c>), vgl. Befund #32.
    /// </summary>
    /// <param name="wert">Zu trimmender Text.</param>
    /// <returns>Der getrimmte Text; leer bei <c>null</c>.</returns>
    public static string Trim(string? wert)
    {
        if (string.IsNullOrEmpty(wert))
        {
            return string.Empty;
        }

        int anfang = 0;
        int ende = wert.Length - 1;
        while (anfang <= ende && wert[anfang] <= ' ')
        {
            anfang++;
        }

        while (ende >= anfang && wert[ende] <= ' ')
        {
            ende--;
        }

        return anfang > ende ? string.Empty : wert.Substring(anfang, ende - anfang + 1);
    }

    /// <summary>Liest ein Datum im Format <c>dd.mm.yyyy</c>. Liefert <c>null</c> bei leerem oder ungültigem Wert.</summary>
    /// <param name="wert">Text im Format <c>dd.mm.yyyy</c>.</param>
    /// <returns>Das Datum oder <c>null</c>.</returns>
    public static DateOnly? DatumLesen(string? wert)
    {
        string text = Trim(wert);
        return DateOnly.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly datum)
            ? datum
            : null;
    }

    /// <summary>
    /// Liest eine Uhrzeit im Format <c>hh:mm</c>. Wie Delphi <c>TimeFromString</c> ergibt ein ungültiger Wert 00:00;
    /// <c>null</c> nur, wenn der Wert fehlt oder leer ist.
    /// </summary>
    /// <param name="wert">Text im Format <c>hh:mm</c>.</param>
    /// <returns>Die Uhrzeit, 00:00 bei ungültigem Wert oder <c>null</c> bei leerem Wert.</returns>
    public static TimeOnly? ZeitLesen(string? wert)
    {
        string text = Trim(wert);
        if (text.Length == 0)
        {
            return null;
        }

        return TimeOnly.TryParseExact(text, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly zeit)
            ? zeit
            : TimeOnly.MinValue;
    }

    /// <summary>Delphi <c>GetAsBool</c>: nur der exakte Wert <c>true</c> ergibt <c>true</c>.</summary>
    /// <param name="wert">Gelesener Text oder <c>null</c>, wenn das Attribut fehlt.</param>
    /// <param name="standard">Ergebnis bei fehlendem Attribut.</param>
    /// <returns>Der Wahrheitswert.</returns>
    public static bool BoolLesen(string? wert, bool standard)
    {
        return wert is null ? standard : Trim(wert) == "true";
    }

    /// <summary>Delphi <c>StrToIntDef</c>.</summary>
    /// <param name="wert">Gelesener Text.</param>
    /// <param name="standard">Ergebnis bei leerem oder ungültigem Wert.</param>
    /// <returns>Die Zahl.</returns>
    public static int ZahlLesen(string? wert, int standard)
    {
        return int.TryParse(Trim(wert), NumberStyles.Integer, CultureInfo.InvariantCulture, out int zahl) ? zahl : standard;
    }

    /// <summary>Delphi <c>DayOfWeekAsString</c> (PlanTypes): deutscher Wochentag, unabhängig von der Systemsprache.</summary>
    /// <param name="datum">Datum.</param>
    /// <returns>Der Wochentag, z. B. <c>Montag</c>.</returns>
    public static string Wochentag(DateTime datum)
    {
        return datum.DayOfWeek switch
        {
            DayOfWeek.Sunday => "Sonntag",
            DayOfWeek.Monday => "Montag",
            DayOfWeek.Tuesday => "Dienstag",
            DayOfWeek.Wednesday => "Mittwoch",
            DayOfWeek.Thursday => "Donnerstag",
            DayOfWeek.Friday => "Freitag",
            _ => "Samstag",
        };
    }

    /// <summary>
    /// Delphi <c>StrToInt</c>: führende Leerzeichen und ein Vorzeichen erlaubt, sonst nur Ziffern.
    /// Ein leerer oder ungültiger Wert löst <see cref="FormatException"/> aus.
    /// </summary>
    /// <param name="wert">Zu lesender Text.</param>
    /// <returns>Die Zahl.</returns>
    /// <exception cref="FormatException">Der Text ist leer oder keine Zahl.</exception>
    public static int StrToInt(string wert)
    {
        ArgumentNullException.ThrowIfNull(wert);
        return int.Parse(wert, NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    /// <summary>Delphi <c>Copy(text, start, laenge)</c> mit 1-basiertem Start: liefert weniger oder nichts, wenn der Text zu kurz ist.</summary>
    /// <param name="text">Ausgangstext.</param>
    /// <param name="start">Erstes Zeichen, 1-basiert.</param>
    /// <param name="laenge">Höchstzahl der Zeichen.</param>
    /// <returns>Der Teiltext.</returns>
    public static string Copy(string text, int start, int laenge)
    {
        ArgumentNullException.ThrowIfNull(text);
        int index = Math.Max(start, 1) - 1;
        return index >= text.Length || laenge <= 0 ? string.Empty : text.Substring(index, Math.Min(laenge, text.Length - index));
    }

    /// <summary>
    /// Delphi <c>DateFromString</c> (PlanUtils): Tag = Zeichen 1–2, Monat = 4–5, Jahr = 7–10, jeweils <c>StrToInt</c>,
    /// dann <c>EncodeDate</c>. Ungültige Werte lösen <see cref="FormatException"/> aus.
    /// </summary>
    /// <param name="text">Text im Format <c>dd.mm.yyyy</c>.</param>
    /// <returns>Das Datum.</returns>
    public static DateOnly DateFromString(string text)
    {
        int tag = StrToInt(Copy(text, 1, 2));
        int monat = StrToInt(Copy(text, 4, 2));
        int jahr = StrToInt(Copy(text, 7, 4));
        if (jahr < 1 || jahr > 9999 || monat < 1 || monat > 12 || tag < 1 || tag > DateTime.DaysInMonth(jahr, monat))
        {
            throw new FormatException($"Ungültiges Datum '{text}'.");
        }

        return new DateOnly(jahr, monat, tag);
    }

    /// <summary>
    /// Delphi <c>TimeFromString</c> (PlanUtils): Stunde = Zeichen 1–2, Minute = 4–5, dann <c>EncodeTime</c>.
    /// Ungültige Werte lösen <see cref="FormatException"/> aus.
    /// </summary>
    /// <param name="text">Text im Format <c>hh:mm</c>.</param>
    /// <returns>Die Uhrzeit.</returns>
    public static TimeOnly TimeFromString(string text)
    {
        int stunde = StrToInt(Copy(text, 1, 2));
        int minute = StrToInt(Copy(text, 4, 2));
        if (stunde < 0 || stunde > 23 || minute < 0 || minute > 59)
        {
            throw new FormatException($"Ungültige Uhrzeit '{text}'.");
        }

        return new TimeOnly(stunde, minute);
    }

    /// <summary>
    /// Delphi <c>TDataObject.GetAsDateTime</c>: Datum aus Zeichen 1–10, Uhrzeit aus 12–16; leer ergibt <c>null</c>
    /// (entspricht 0.0), ebenso der Nullpunkt selbst. Ungültige Werte lösen <see cref="FormatException"/> aus.
    /// </summary>
    /// <param name="text">Text im Format <c>dd.mm.yyyy hh:mm</c>.</param>
    /// <returns>Der Zeitpunkt oder <c>null</c> für „kein Termin“.</returns>
    public static DateTime? DatumZeitStreng(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return null;
        }

        TimeOnly zeit = TimeFromString(Copy(text, 12, 5));
        DateTime wert = DateFromString(Copy(text, 1, 10)).ToDateTime(zeit);
        return wert == DelphiNull ? null : wert;
    }

    /// <summary>Delphi <c>DateWithTimeToString</c>: <c>dd.mm.yyyy hh:mm</c>; <c>null</c> wird zum Nullpunkt 30.12.1899 00:00.</summary>
    /// <param name="wert">Zeitpunkt oder <c>null</c>.</param>
    /// <returns>Der Text im Format <c>dd.mm.yyyy hh:mm</c>.</returns>
    public static string DatumZeitSchreiben(DateTime? wert) =>
        (wert ?? DelphiNull).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
