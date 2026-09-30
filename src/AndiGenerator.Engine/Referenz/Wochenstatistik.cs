// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Anzahl Spiele pro Woche einer Runde (Original <c>PaintGamesPerWeekOneRound</c>).</summary>
/// <param name="Titel"><c>Hinrunde</c> oder <c>Rückrunde</c>.</param>
/// <param name="Montage">Montag jeder Spielwoche.</param>
/// <param name="Spiele">Je Mannschaft (Reihenfolge der Staffel) die Spiele je Woche.</param>
/// <param name="MaxDifferenz">Je Woche die größte Differenz der bis dahin absolvierten Spiele.</param>
public sealed record Wochenstatistik(
    string Titel,
    IReadOnlyList<DateOnly> Montage,
    IReadOnlyList<IReadOnlyList<int>> Spiele,
    IReadOnlyList<int> MaxDifferenz);
