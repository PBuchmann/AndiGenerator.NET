// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Platz einer Mannschaft in der Setzliste (Original <c>ranking/team</c>).</summary>
/// <param name="Mannschaft">Name der Mannschaft (<c>teamname</c>).</param>
/// <param name="Platz">Platz, 0-basiert (<c>rankingindex</c>).</param>
public sealed record Setzlisteneintrag(string Mannschaft, int Platz);
