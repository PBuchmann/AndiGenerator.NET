// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Fortschritt von <see cref="Ligaschranke"/>: woran gerechnet wird und der bisher beste Plan.</summary>
/// <param name="Text">Woran gerechnet wird, z. B. <c>A3 Parallele Spiele</c>.</param>
/// <param name="Schritt">Erledigte Schritte.</param>
/// <param name="Schritte">Alle Schritte (Stufen A und B, dazu Stufe C mit dem Feinschliff).</param>
/// <param name="Plan">Der bisher beste Plan (nach der zuletzt erledigten Stufe), oder <c>null</c>.</param>
/// <param name="Zwischenbericht">
/// Sind A und B fertig, schon vor der Stufe C der Bericht mit ihren Schranken und den Varianten in B (zum Wählen, während C
/// noch rechnet); sonst <c>null</c>.
/// </param>
public sealed record Schrankenfortschritt(string Text, int Schritt, int Schritte, IReadOnlyList<Spiel>? Plan, Schrankenbericht? Zwischenbericht = null);
