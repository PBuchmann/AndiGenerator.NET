// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Ein Kriterium im besten Plan: bei der Kostenoptimierung mit seinen Kosten, im Automodus mit seinen Verstößen und seiner
/// Stufe. Aus zwei aufeinanderfolgenden besten Plänen zeigt der Maschinenraum, was eine Verbesserung geändert hat.
/// </summary>
/// <param name="Kriterium">Das Kriterium.</param>
/// <param name="Wert">Kostenoptimierung: seine Kosten; Automodus: die Zahl seiner Verstöße.</param>
/// <param name="Stufe">Im Automodus seine Stufe (A, B oder C); sonst <c>null</c>.</param>
public sealed record Kriterienwert(Kostenkriterium Kriterium, double Wert, Stufe? Stufe = null);
