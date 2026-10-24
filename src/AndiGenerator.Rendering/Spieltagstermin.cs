// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Eine Woche eines Spieltags: die Mannschaften, die in ihr ihr Spiel des Spieltags austragen.</summary>
/// <param name="Woche">Montag der Woche.</param>
/// <param name="Spiele">Die Mannschaften mit Tag und Heimrecht, nach Tag geordnet.</param>
public sealed record Spieltagstermin(DateOnly Woche, IReadOnlyList<Spieltagsmannschaft> Spiele)
{
    /// <summary>Holt die Anzahl der Mannschaften.</summary>
    public int Mannschaften => Spiele.Count;
}
