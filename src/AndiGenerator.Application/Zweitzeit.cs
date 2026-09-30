// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Application;

/// <summary>Heimspieltermin mit alternativer Anfangszeit für Auswärtskoppeln (Original <c>ValueListSecondTime</c>).</summary>
/// <param name="Termin">Heimspieltermin.</param>
/// <param name="Zeit">Alternative Anfangszeit oder <c>null</c>.</param>
public sealed record Zweitzeit(DateTime Termin, TimeOnly? Zeit)
{
    /// <summary>
    /// Vorschlag für „Alles setzen“ wie im Original: vier Stunden früher bei Terminen nach 16:59 Uhr, sonst vier Stunden später.
    /// </summary>
    public TimeOnly Vorschlag
    {
        get
        {
            var zeit = TimeOnly.FromDateTime(Termin);
            return zeit > new TimeOnly(16, 59) ? zeit.AddHours(-4) : zeit.AddHours(4);
        }
    }

    /// <summary>Liest eine Uhrzeit wie das Original (<c>TimeFromString</c>): nur <c>hh:mm</c>, sonst und bei 00:00 keine Zeit.</summary>
    /// <param name="text">Eingabe.</param>
    /// <returns>Die Uhrzeit oder <c>null</c>.</returns>
    public static TimeOnly? Lesen(string? text)
    {
        string t = (text ?? string.Empty).Trim();
        if (t.Length < 5 || t[2] != ':'
            || !int.TryParse(t.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int stunde)
            || !int.TryParse(t.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int minute)
            || stunde > 23 || minute > 59 || (stunde == 0 && minute == 0))
        {
            return null;
        }

        return new TimeOnly(stunde, minute);
    }
}
