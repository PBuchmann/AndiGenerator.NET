// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Ein Kriterium im Dialog der B-Varianten: beim aktuellen Plan seine Verstöße je Mannschaft, bei einer Variante die Unterschiede
/// zum aktuellen Plan (was neu dazukommt, was entfällt).
/// </summary>
/// <param name="Titel">Z. B. <c>B1 · Auswärtskoppel: 5 → 4</c>.</param>
/// <param name="Gruppen">Je Mannschaft die Verstöße bzw. Unterschiede.</param>
public sealed record Vergleichsabschnitt(string Titel, IReadOnlyList<Unterschiedsgruppe> Gruppen);
