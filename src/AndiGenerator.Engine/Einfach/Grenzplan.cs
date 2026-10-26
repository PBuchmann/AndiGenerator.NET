// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Einfach;

/// <summary>Ein Plan an der Grenze des Machbaren.</summary>
/// <param name="Plan">Der Plan.</param>
/// <param name="Guete">Seine Verstöße.</param>
/// <param name="Gruppen">Je Gruppe ihr Wert (nach dem Bezug des Maßstabs).</param>
public sealed record Grenzplan(IReadOnlyList<Spiel> Plan, Planguete Guete, IReadOnlyList<double> Gruppen);
