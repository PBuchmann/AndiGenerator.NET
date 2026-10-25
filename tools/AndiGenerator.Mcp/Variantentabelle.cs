// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Der Dialog der B-Varianten als Tabelle.</summary>
/// <param name="Hinweis">Hinweis (z. B. ausgeblendete Varianten).</param>
/// <param name="KannFestlegen">Festlegen ist möglich (nur während der Generierung im Automodus).</param>
/// <param name="Spalten">Spalten: zuerst der aktuelle Plan, dann die Varianten mit ihren Summen.</param>
/// <param name="Zeilen">Je Kriterium bzw. Summe die Werte der Spalten, z. B. „4 (−1)“.</param>
public sealed record Variantentabelle(string Hinweis, bool KannFestlegen, IReadOnlyList<string> Spalten, IReadOnlyList<string> Zeilen);
