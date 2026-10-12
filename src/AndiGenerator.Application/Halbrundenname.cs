// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.RegularExpressions;

namespace AndiGenerator.Application;

/// <summary>
/// Erkennt am Klassennamen, ob eine Klasse nur eine Halbrunde spielt (nicht im Original): Der Verband kennzeichnet sie mit
/// „(HR)“ für Hinrunde bzw. Herbstrunde und „(FR)“ für Finalrunde bzw. Frühlingsrunde.
/// </summary>
public static partial class Halbrundenname
{
    /// <summary>Liest das Kennzeichen aus dem Klassennamen.</summary>
    /// <param name="name">Name der Staffel, z. B. „Jugend 19 Verbandsoberliga (HR)“.</param>
    /// <returns>Das Kennzeichen oder <see cref="Halbrundenkennzeichen.Keins"/>.</returns>
    public static Halbrundenkennzeichen Kennzeichen(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Match treffer = Muster().Match(name);
        if (!treffer.Success)
        {
            return Halbrundenkennzeichen.Keins;
        }

        return string.Equals(treffer.Groups[1].Value, "HR", StringComparison.OrdinalIgnoreCase)
            ? Halbrundenkennzeichen.Hinrunde
            : Halbrundenkennzeichen.Rueckrunde;
    }

    [GeneratedRegex(@"\(\s*(HR|FR)\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Muster();
}
