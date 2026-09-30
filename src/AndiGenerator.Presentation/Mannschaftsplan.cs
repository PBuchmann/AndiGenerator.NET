// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Der Plan einer Mannschaft als Kachel.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Bilanz">Z. B. „11 Heim · 11 Auswärts“.</param>
/// <param name="Folge">Heim (<c>true</c>) und Auswärts (<c>false</c>) in Spielreihenfolge, nur Spiele mit Termin.</param>
/// <param name="Spiele">Die Spiele.</param>
public sealed record Mannschaftsplan(string Name, string Bilanz, IReadOnlyList<bool> Folge, IReadOnlyList<Mannschaftsspiel> Spiele);
