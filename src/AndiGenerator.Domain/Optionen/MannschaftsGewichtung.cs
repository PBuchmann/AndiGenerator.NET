// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Optionen;

/// <summary>Gewichtungen einer einzelnen Mannschaft (Original <c>TGewichtungMannschaften</c>, ein Eintrag).</summary>
/// <param name="MannschaftsId">click-TT-Id der Mannschaft.</param>
/// <param name="Gesamt">Gewichtung aller Kosten dieser Mannschaft (<c>main</c>).</param>
/// <param name="JeKostenart">Gewichtung je <see cref="MannschaftsKostenart"/> (Länge 16, Index = Enumwert).</param>
public sealed record MannschaftsGewichtung(string MannschaftsId, Gewichtung Gesamt, IReadOnlyList<Gewichtung> JeKostenart);
