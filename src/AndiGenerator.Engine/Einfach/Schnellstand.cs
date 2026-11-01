// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Einfach;

/// <summary>Ein Stand des einfachen Modus für die Anzeige.</summary>
/// <param name="Sekunden">Sekunden seit dem Start.</param>
/// <param name="Phase">1 = A und B mit HiGHS, 2 = Proben, 3 = Rennen der Kandidaten.</param>
/// <param name="Text">Was gerade geschieht (oder was eben gefunden wurde).</param>
/// <param name="Bester">Der beste Plan für die Wahl des Klassenleiters und seine Verstöße, oder <c>null</c>.</param>
/// <param name="Kandidaten">Die Kandidaten des Rennens (leer bis Phase 3).</param>
/// <param name="Grenze">Die Pläne an der Grenze des Machbaren (leer bis Phase 3).</param>
/// <param name="Massstab">Der Maßstab des Augenblicks (Gewichte, Fokus, Bezug).</param>
/// <param name="Stufe">Die Stufe (1 bis zur ersten Wahl des Klassenleiters).</param>
/// <param name="Vorschlaege">Die Vorschläge zur Wahl (ab Phase 3), oder <c>null</c>.</param>
/// <param name="Status">Je Kriterium des besten Plans, was bewiesen ist, oder <c>null</c>.</param>
/// <param name="SchrankeA">Bewiesene Untergrenze der Pflicht (Summe), oder <c>null</c>.</param>
/// <param name="SchrankeB">Bewiesene Untergrenze der Wünsche (Summe, bei bestmöglicher Pflicht), oder <c>null</c>.</param>
public sealed record Schnellstand(
    double Sekunden,
    int Phase,
    string Text,
    (IReadOnlyList<Spiel> Plan, Planguete Guete)? Bester,
    IReadOnlyList<Schnellkandidat> Kandidaten,
    IReadOnlyList<Grenzplan> Grenze,
    Massstab Massstab,
    int Stufe = 1,
    IReadOnlyList<Vorschlag>? Vorschlaege = null,
    IReadOnlyList<Kriteriumsstatus>? Status = null,
    int? SchrankeA = null,
    int? SchrankeB = null);
