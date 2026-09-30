// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Auswärtskoppelwunsch: die Mannschaft möchte bei zwei Gegnern gekoppelt antreten (Original <c>roadcouple</c>).</summary>
/// <param name="MannschaftA">Erster Gegner (<c>teamnamea</c>).</param>
/// <param name="MannschaftB">Zweiter Gegner (<c>teamnameb</c>).</param>
/// <param name="Art">Gewünschte Art der Koppelung (<c>sameday</c>).</param>
public sealed record Auswaertskoppel(string MannschaftA, string MannschaftB, Auswaertskoppelart Art);
