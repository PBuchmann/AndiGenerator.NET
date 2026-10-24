// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Verstöße einer Mannschaft.</summary>
/// <param name="Mannschaft">Name.</param>
/// <param name="Anzahl">Anzahl als Text.</param>
/// <param name="Einzelheiten">Die einzelnen Verstöße.</param>
public sealed record Mannschaftsverstoesse(string Mannschaft, string Anzahl, IReadOnlyList<string> Einzelheiten);
