// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile (Mannschaft) der Kostenmatrix.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Marke">Gewichtungsmarke der Mannschaft oder leer.</param>
/// <param name="Gesamt">Gesamtkosten der Mannschaft.</param>
/// <param name="Balken">Kosten relativ zur teuersten Mannschaft (0–1).</param>
/// <param name="Gewaehlt">Die Mannschaft ist ausgewählt.</param>
/// <param name="Zellen">Die Zellen in der Reihenfolge der Spalten.</param>
public sealed record Matrixzeile(string Name, string Marke, string Gesamt, double Balken, bool Gewaehlt, IReadOnlyList<Matrixzelle> Zellen);
