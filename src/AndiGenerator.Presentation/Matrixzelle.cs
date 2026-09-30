// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Eine Zelle der Kostenmatrix.</summary>
/// <param name="Mannschaft">Name der Mannschaft.</param>
/// <param name="Kostenart">Die Kostenart.</param>
/// <param name="Text">Kosten oder Verstöße (je nach Anzeige); „·“ = eingehalten.</param>
/// <param name="Anteil">Anteil an den Gesamtkosten (0–1) für die Einfärbung.</param>
/// <param name="Geaendert">Die Gewichtung dieser Zelle ist geändert.</param>
/// <param name="Gewaehlt">Die Zelle ist ausgewählt.</param>
/// <param name="Hinweis">Tooltip mit allen Werten wie im Original.</param>
public sealed record Matrixzelle(string Mannschaft, MannschaftsKostenart Kostenart, string Text, double Anteil, bool Geaendert, bool Gewaehlt, string Hinweis);
