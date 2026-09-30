// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Ergebnis eines Optimierungslaufs.</summary>
/// <param name="Kosten">Kosten des besten Plans (Original <c>CalculateKosten(-1)</c>).</param>
/// <param name="Verbesserungen">Anzahl der akzeptierten Verbesserungen.</param>
/// <param name="Spiele">Der beste Plan: alle Spiele, <see cref="Spiel.Zeitpunkt"/> = <c>null</c> für nicht terminierte.</param>
public sealed record Optimierungsergebnis(double Kosten, int Verbesserungen, IReadOnlyList<Spiel> Spiele);
