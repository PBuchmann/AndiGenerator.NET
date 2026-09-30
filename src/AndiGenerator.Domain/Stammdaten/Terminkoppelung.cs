// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Art der Koppelung eines Heimspieltermins.</summary>
public enum Terminkoppelung
{
    /// <summary>Einzeltermin.</summary>
    Keine,

    /// <summary>Koppeltermin: zwei Heimspiele am selben Tag (<c>couplegameday</c>).</summary>
    Koppeltermin,

    /// <summary>Doppelspieltag: Heimspiele an zwei aufeinanderfolgenden Tagen (<c>doublegameday</c>).</summary>
    Doppelspieltag,
}
