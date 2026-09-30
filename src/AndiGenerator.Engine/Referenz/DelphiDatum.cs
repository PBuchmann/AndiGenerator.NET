// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Delphi-<c>TDateTime</c> als <see cref="double"/>: ganze Tage seit 30.12.1899 plus Tagesbruchteil. Werte werden genau
/// wie im Original gebildet (<c>EncodeDate</c> + <c>EncodeTime</c>), damit Gleichheitsvergleiche dieselben Ergebnisse liefern.
/// </summary>
internal static class DelphiDatum
{
    /// <summary>Mindestabstand zweier Spiele am selben Tag (Original <c>MinGameDistance</c> = 3:30 h).</summary>
    public static readonly double MinGameDistance = EncodeTime(3, 30);

    private static readonly int NullpunktTagnummer = new DateOnly(1899, 12, 30).DayNumber;

    /// <summary>Delphi <c>EncodeTime(h, m, 0, 0)</c>.</summary>
    public static double EncodeTime(int stunde, int minute) => ((stunde * 3600000) + (minute * 60000)) / 86400000.0;

    /// <summary>Delphi <c>EncodeDate</c>.</summary>
    public static double Wert(DateOnly datum) => datum.DayNumber - NullpunktTagnummer;

    /// <summary>Delphi <c>EncodeDate + EncodeTime</c> (Sekunden werden wie im Dateiformat ignoriert).</summary>
    public static double Wert(DateTime zeitpunkt) => Wert(DateOnly.FromDateTime(zeitpunkt)) + EncodeTime(zeitpunkt.Hour, zeitpunkt.Minute);

    /// <summary>Umkehrung von <see cref="Wert(DateTime)"/> (auf Minuten gerundet).</summary>
    public static DateTime ZuDateTime(double wert)
    {
        int tag = Trunc(wert);
        int minuten = (int)Math.Round((wert - tag) * 1440.0, MidpointRounding.AwayFromZero);
        return DateOnly.FromDayNumber(tag + NullpunktTagnummer).ToDateTime(TimeOnly.MinValue).AddMinutes(minuten);
    }

    /// <summary>Delphi <c>Trunc</c>.</summary>
    public static int Trunc(double wert) => (int)Math.Truncate(wert);

    /// <summary>Delphi <c>DayOfWeek</c>: 1 = Sonntag … 7 = Samstag.</summary>
    public static int Wochentag(double wert) => (int)DateOnly.FromDayNumber(Trunc(wert) + NullpunktTagnummer).DayOfWeek + 1;

    /// <summary>Original <c>InternalWeekNumber</c> (Woche Montag bis Sonntag).</summary>
    public static int InterneWochennummer(double wert) => (Trunc(wert) - 2) / 7;

    /// <summary>Original <c>MondayBefore</c>.</summary>
    public static double MontagDavor(double wert) => (((Trunc(wert) - 2) / 7) * 7) + 2;

    /// <summary>Datum als Text wie <c>MyFormatDate</c> (für Meldungen), z. B. <c>Mi 23.09.2026</c>.</summary>
    public static string Text(double wert)
    {
        DateOnly tag = DateOnly.FromDayNumber(Trunc(wert) + NullpunktTagnummer);
        string[] namen = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];
        return namen[(int)tag.DayOfWeek] + " " + tag.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}
