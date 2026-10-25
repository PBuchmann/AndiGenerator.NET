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
/// <param name="SchrankeA">
/// Gleich nach Stufe A ihre bewiesene Schranke (die Summe der A-Verstöße, die jeder Plan mindestens hat), oder <c>null</c> – der
/// Automodus hört damit sofort auf, A zu drängen, statt auf den Zwischenbericht nach Stufe B zu warten.
/// </param>
/// <param name="AFertig">
/// Stufe A ist fertig (bewiesen oder beim besten gefundenen Wert festgehalten): Die Teilneuplanung muss nicht länger auf die
/// Untergrenze warten – sie senkt B von hier an meist schneller als die weiteren Schritte der Kette.
/// </param>
/// <param name="NurPlan">
/// Nur ein besserer Plan mitten in einem Schritt (HiGHS hat ihn eben gefunden): anbieten, an der Anzeige des Schritts nichts
/// ändern.
/// </param>
public sealed record Schrankenfortschritt(string Text, int Schritt, int Schritte, IReadOnlyList<Spiel>? Plan, Schrankenbericht? Zwischenbericht = null, int? SchrankeA = null, bool AFertig = false, bool NurPlan = false);
