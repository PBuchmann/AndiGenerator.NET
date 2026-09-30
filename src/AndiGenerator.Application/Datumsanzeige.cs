// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Application;

/// <summary>
/// Datumsanzeige wie im Original (<c>MyFormatDate</c> = <c>ddd dd.mm.yyyy</c>, z. B. <c>Sa 05.09.2026</c>), unabhängig von
/// der Systemsprache und ohne den Punkt, den .NET bei deutschen Kurznamen anhängt.
/// </summary>
public static class Datumsanzeige
{
    /// <summary>Tag mit Wochentag, z. B. <c>Sa 05.09.2026</c>.</summary>
    /// <param name="tag">Der Tag.</param>
    /// <returns>Der Text.</returns>
    public static string Tag(DateOnly tag) =>
        DelphiKompatibel.Wochentag(tag.ToDateTime(TimeOnly.MinValue))[..2] + tag.ToString(" dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Termin mit Wochentag und Uhrzeit, z. B. <c>Sa 05.09.2026 18:00</c>.</summary>
    /// <param name="termin">Der Termin.</param>
    /// <returns>Der Text.</returns>
    public static string Termin(DateTime termin) =>
        Tag(DateOnly.FromDateTime(termin)) + termin.ToString(" HH:mm", CultureInfo.InvariantCulture);
}
