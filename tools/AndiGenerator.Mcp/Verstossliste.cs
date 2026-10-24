// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Verstöße eines Kriteriums je Mannschaft und was es kostet, das Kriterium bestmöglich zu machen.</summary>
/// <param name="Titel">Stufe und Kriterium.</param>
/// <param name="Mannschaften">Je Mannschaft die Verstöße.</param>
/// <param name="Folge">Folgen der besten Variante für dieses Kriterium (leer, wenn keine besser ist).</param>
public sealed record Verstossliste(string Titel, IReadOnlyList<Mannschaftsverstoesse> Mannschaften, IReadOnlyList<string> Folge);
