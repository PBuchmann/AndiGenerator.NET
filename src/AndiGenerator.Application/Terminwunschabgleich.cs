// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Application;

/// <summary>Ergebnis des Abgleichs der Spielplandaten mit dem Bericht „Terminwünsche“ aus click-TT.</summary>
/// <param name="Meldungen">Die gelesenen Meldungen.</param>
/// <param name="Mannschaften">Je Mannschaft der Staffel der Abgleich.</param>
public sealed record Terminwunschabgleich(IReadOnlyList<Vereinsmeldung> Meldungen, IReadOnlyList<Mannschaftsabgleich> Mannschaften)
{
    /// <summary>Holt die Zahl der Heimtermine, deren Spiellokal-Nummer gesetzt wird.</summary>
    public int NummernGesetzt { get; } = Mannschaften.Sum(m => m.NummernGesetzt);
}
