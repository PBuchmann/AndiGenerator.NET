// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>Mannschaft desselben Vereins in einer anderen Staffel (<c>sisterTeams/team</c>) mit ihren Spielen.</summary>
/// <param name="Nummer">Attribut <c>number</c>, 0 wenn nicht angegeben (wie im Original).</param>
public sealed record ClickTtNachbarmannschaft(
    string Name,
    string Geschlecht,
    int Nummer,
    IReadOnlyList<ClickTtSpiel> Spiele);
