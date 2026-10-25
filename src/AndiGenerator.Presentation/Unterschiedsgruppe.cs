// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Die Verstöße bzw. Unterschiede einer Mannschaft bei einem Kriterium (Dialog der B-Varianten).</summary>
/// <param name="Name">Die Mannschaft.</param>
/// <param name="Anzahl">Z. B. <c>2 Verstöße</c> oder <c>+1 neu · −2 entfallen</c>.</param>
/// <param name="Zeilen">Die Meldungen.</param>
public sealed record Unterschiedsgruppe(string Name, string Anzahl, IReadOnlyList<Unterschied> Zeilen);
