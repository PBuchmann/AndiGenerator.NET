// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Zeitraum, in dem jede Mannschaft eine Mindestzahl an Spielen haben muss (Original <c>mandatorygames</c>).</summary>
/// <param name="Von">Erster Tag (<c>datefrom</c>).</param>
/// <param name="Bis">Letzter Tag, einschließlich (<c>dateto</c>).</param>
/// <param name="AnzahlSpiele">Mindestzahl der Spiele je Mannschaft (<c>numbergames</c>).</param>
public sealed record Pflichtspielzeitraum(DateOnly Von, DateOnly Bis, int AnzahlSpiele);
