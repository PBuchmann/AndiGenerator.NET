// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>Heimspielwunsch (<c>homeGame</c>).</summary>
/// <param name="Ausweichtermin">Attribut <c>secondary</c>.</param>
/// <param name="MaxParalleleSpiele">Attribut <c>maxParallelGames</c>, 0 wenn nicht angegeben.</param>
/// <param name="Koppelwunsch">Attribut <c>coupleGame</c> (z.B. <c>soft</c>, <c>hard</c>), <c>null</c> wenn nicht angegeben.</param>
public sealed record ClickTtHeimspieltermin(
    DateTime Zeitpunkt,
    bool Ausweichtermin,
    int MaxParalleleSpiele,
    string? Koppelwunsch,
    string Halle);
