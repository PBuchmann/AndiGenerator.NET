// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Setzliste der Staffel (Original <c>ranking</c>).</summary>
/// <param name="Aktiv">Die Setzliste wird berücksichtigt (<c>active</c>).</param>
/// <param name="Eintraege">Einträge in Dateireihenfolge.</param>
public sealed record Setzliste(bool Aktiv, IReadOnlyList<Setzlisteneintrag> Eintraege);
