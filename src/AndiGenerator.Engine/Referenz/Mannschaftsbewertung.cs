// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Kosten einer Mannschaft.</summary>
/// <param name="Name">Mannschaftsname.</param>
/// <param name="JeKostenart">Werte je <see cref="MannschaftsKostenart"/> (Index = Enumwert).</param>
/// <param name="Gesamt">Summe aller Kostenarten.</param>
/// <param name="Meldungen">Klartextmeldungen.</param>
public sealed record Mannschaftsbewertung(string Name, IReadOnlyList<Kostenzelle> JeKostenart, double Gesamt, IReadOnlyList<string> Meldungen);
