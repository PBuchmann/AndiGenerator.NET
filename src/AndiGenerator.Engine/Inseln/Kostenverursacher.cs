// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Ein großer Verursacher im besten Plan einer Insel: bei der Kostenoptimierung ein Kriterium mit seinem Anteil an den
/// Kosten, im Automodus ein Kriterium mit seinen Verstößen.
/// </summary>
/// <param name="Kriterium">Das Kriterium.</param>
/// <param name="Anteil">Kostenoptimierung: Anteil an den Kosten aller Kriterien; Automodus: Verstöße im Verhältnis zum größten (0 bis 1).</param>
/// <param name="Verstoesse">Im Automodus die Zahl der Verstöße; sonst <c>null</c>.</param>
/// <param name="Stufe">Im Automodus die Stufe des Kriteriums; sonst <c>null</c>.</param>
public sealed record Kostenverursacher(Kostenkriterium Kriterium, double Anteil, int? Verstoesse = null, Stufe? Stufe = null);
