// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Eine Woche des Terminplans.</summary>
/// <param name="Woche">Titel, z. B. KW 37.</param>
/// <param name="Zeitraum">Montag bis Sonntag.</param>
/// <param name="Rundenwechsel">Beginn einer neuen Runde vor der Woche oder leer.</param>
/// <param name="Spiele">Die Spiele: Nummer, Tag, Datum, Uhrzeit, Heim – Gast, Runde und Hinweise.</param>
public sealed record Planwoche(string Woche, string Zeitraum, string Rundenwechsel, IReadOnlyList<string> Spiele);
