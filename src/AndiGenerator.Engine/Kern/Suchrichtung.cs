// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Kern;

/// <summary>Suchrichtung bei parallelen Spielen benachbarter Mannschaftsnummern (Original <c>GetParallelGames</c>).</summary>
internal enum Suchrichtung
{
    Beide,
    Hoeher,
    Tiefer,
}
