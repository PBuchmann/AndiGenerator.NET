// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Daten einer Mannschaft für die Diagramme.</summary>
/// <param name="Name">Mannschaftsname.</param>
/// <param name="Spiele">Spiele nach Termin, nicht terminierte zuerst.</param>
/// <param name="Abstaende">Je Gegner eine Zeile mit den Linien zwischen den Spielen gegen ihn.</param>
public sealed record Diagrammmannschaft(string Name, IReadOnlyList<Diagrammspiel> Spiele, IReadOnlyList<IReadOnlyList<Abstandslinie>> Abstaende);
