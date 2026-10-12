// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Eine Mannschaft im Abgleich mit dem Bericht „Terminwünsche“: Spiellokale, Bemerkung, Unterschiede.</summary>
/// <param name="Mannschaft">Name der Mannschaft.</param>
/// <param name="Gefunden">Die Mannschaft steht im Bericht.</param>
/// <param name="Spiellokale">Spiellokale des Vereins.</param>
/// <param name="Bemerkung">Bemerkung des Vereins, leer wenn keine.</param>
/// <param name="Abweichungen">Unterschiede zu den Spielplandaten.</param>
public sealed record Vereinskarte(
    string Mannschaft,
    bool Gefunden,
    IReadOnlyList<Spiellokalangabe> Spiellokale,
    string Bemerkung,
    IReadOnlyList<string> Abweichungen)
{
    /// <summary>Holt einen Wert, der angibt, ob es eine Bemerkung gibt.</summary>
    public bool HatBemerkung => Bemerkung.Length > 0;

    /// <summary>Holt einen Wert, der angibt, ob es Unterschiede gibt.</summary>
    public bool HatAbweichungen => Abweichungen.Count > 0;

    /// <summary>Holt die Marke rechts im Kopf, z. B. „3 Unterschiede“, „nicht im Bericht“ oder „stimmt überein“.</summary>
    public string Marke => (Gefunden, Abweichungen.Count) switch
    {
        (false, _) => "nicht im Bericht",
        (true, 0) => "stimmt überein",
        (true, 1) => "1 Unterschied",
        _ => Abweichungen.Count.ToString(CultureInfo.InvariantCulture) + " Unterschiede",
    };
}
