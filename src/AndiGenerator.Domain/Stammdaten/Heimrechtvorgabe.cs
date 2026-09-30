// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Vorgabe, in welcher Runde die Mannschaft gegen einen Gegner Heimrecht hat (Original <c>homerights</c>-Knoten).</summary>
/// <param name="Gegner">Name des Gegners (<c>teamname</c>).</param>
/// <param name="Runde">Runde mit Heimrecht (<c>homeright</c>).</param>
public sealed record Heimrechtvorgabe(string Gegner, Runde Runde);
