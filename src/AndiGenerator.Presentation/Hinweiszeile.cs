// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Hinweis zu einer Mannschaft, z. B. ihre Bemerkung aus click-TT oder eine Abweichung.</summary>
/// <param name="Mannschaft">Name der Mannschaft.</param>
/// <param name="Text">Der Hinweis.</param>
public sealed record Hinweiszeile(string Mannschaft, string Text);
