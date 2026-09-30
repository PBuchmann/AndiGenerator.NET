// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Heimrecht einer Mannschaft (Original <c>TDialogPanelHomeRightDetail</c>).</summary>
/// <param name="HeimspieleVorrunde">Gewünschte Zahl Heimspiele in der Vorrunde, −1 = automatisch (<c>team@homerights</c>).</param>
/// <param name="Gegner">Heimrecht je Gegner: 0 = egal, 1 = Vorrunde, 2 = Rückrunde (<c>homerights@homeright</c>).</param>
public sealed record Heimrecht(int HeimspieleVorrunde, IReadOnlyDictionary<string, int> Gegner)
{
    /// <summary>Wert für „Automatisch“.</summary>
    public const int Automatisch = -1;

    /// <summary>Wert für „egal“.</summary>
    public const int Egal = 0;

    /// <summary>Wert für „Vorrunde“.</summary>
    public const int Vorrunde = 1;

    /// <summary>Wert für „Rückrunde“.</summary>
    public const int Rueckrunde = 2;
}
