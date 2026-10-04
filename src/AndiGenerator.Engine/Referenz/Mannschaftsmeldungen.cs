// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Meldungen einer Mannschaft zu einer Kostenart.</summary>
/// <param name="Mannschaft">Name der Mannschaft.</param>
/// <param name="Meldungen">Die Meldungen; leer, wenn keine anfallen.</param>
public sealed record Mannschaftsmeldungen(string Mannschaft, IReadOnlyList<string> Meldungen);
