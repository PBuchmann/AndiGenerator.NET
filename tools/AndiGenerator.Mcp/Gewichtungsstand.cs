// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Eine Gewichtung.</summary>
/// <param name="Ebene">„Plan“, „Kostenart“, „Mannschaft“ oder „Mannschaft und Kostenart“.</param>
/// <param name="Kostenart">Name der Kostenart (leer bei „Mannschaft“).</param>
/// <param name="Mannschaft">Name der Mannschaft (leer bei „Plan“ und „Kostenart“).</param>
/// <param name="Stufe">Die Stufe, z. B. „normal“.</param>
public sealed record Gewichtungsstand(string Ebene, string Kostenart, string Mannschaft, string Stufe);
