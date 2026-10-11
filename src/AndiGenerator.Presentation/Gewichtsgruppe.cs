// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Kriterium mit den Gewichten, die der Automodus dafür geändert hat (Maschinenraum).</summary>
/// <param name="Stufe">Stufe mit Nummer, z. B. <c>A2</c>.</param>
/// <param name="Kriterium">Name des Kriteriums.</param>
/// <param name="Zeilen">Je geändertes Gewicht eine Zeile (ganzes Kriterium bzw. je Mannschaft).</param>
public sealed record Gewichtsgruppe(string Stufe, string Kriterium, IReadOnlyList<Gewichtszeile> Zeilen);
