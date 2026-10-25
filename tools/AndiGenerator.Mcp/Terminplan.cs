// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Der Terminplan eines Plans, wochenweise.</summary>
/// <param name="Plan">Der gezeigte Plan.</param>
/// <param name="Zusammenfassung">Zahl der Spiele, davon ohne Termin.</param>
/// <param name="Wochen">Die Wochen mit ihren Spielen.</param>
/// <param name="OhneTermin">Spiele ohne Termin.</param>
/// <param name="Plaene">Die Pläne, die es gibt.</param>
public sealed record Terminplan(string Plan, string Zusammenfassung, IReadOnlyList<Planwoche> Wochen, IReadOnlyList<string> OhneTermin, IReadOnlyList<string> Plaene);
