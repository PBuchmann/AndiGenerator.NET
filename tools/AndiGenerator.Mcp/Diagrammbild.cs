// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Ein gezeichnetes Diagramm mit Kennzahlen.</summary>
/// <param name="Titel">Titel des Diagramms.</param>
/// <param name="Beschreibung">Was es zeigt.</param>
/// <param name="Plan">Der gezeigte Plan.</param>
/// <param name="Kennzahlen">Verstöße je Mannschaft.</param>
/// <param name="Diagramme">Die Diagramme, die es für den Plan gibt.</param>
/// <param name="Png">Das Bild (PNG) oder leer.</param>
public sealed record Diagrammbild(string Titel, string Beschreibung, string Plan, IReadOnlyList<string> Kennzahlen, IReadOnlyList<string> Diagramme, byte[] Png);
