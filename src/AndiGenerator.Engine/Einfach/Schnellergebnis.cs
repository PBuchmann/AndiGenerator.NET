// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Schranke;

namespace AndiGenerator.Engine.Einfach;

/// <summary>Ergebnis des einfachen Modus.</summary>
/// <param name="Plan">Der beste Plan für die Wahl des Klassenleiters (leer, wenn keiner gefunden wurde).</param>
/// <param name="Guete">Seine Güte, oder <c>null</c>.</param>
/// <param name="Grundplan">Der Plan nach A und B (Phase 1) und seine Güte, oder <c>null</c>.</param>
/// <param name="Proben">Die Proben der C-Kriterien (Phase 2).</param>
/// <param name="Kandidaten">Die Kandidaten des Rennens, bester zuerst.</param>
/// <param name="ErsterGueltigerPlan">Sekunden bis zum ersten Plan ohne harte Fehler, oder <c>null</c>.</param>
/// <param name="AUndBFertig">Sekunden, bis HiGHS A und B fertig hatte, oder <c>null</c>.</param>
/// <param name="Protokoll">Die Schritte mit Zeit.</param>
/// <param name="Grenze">Die Pläne an der Grenze des Machbaren.</param>
/// <param name="Massstab">Der Maßstab am Ende (Gewichte, Fokus, Bezug).</param>
/// <param name="Stufe">Die Stufe am Ende.</param>
/// <param name="Vorschlaege">Die Vorschläge am Ende, oder <c>null</c>.</param>
/// <param name="Status">Je Kriterium des besten Plans, was bewiesen ist, oder <c>null</c>.</param>
/// <param name="SchrankeA">Bewiesene Untergrenze der Pflicht (Summe), oder <c>null</c>.</param>
/// <param name="SchrankeB">Bewiesene Untergrenze der Wünsche (Summe, bei bestmöglicher Pflicht), oder <c>null</c>.</param>
public sealed record Schnellergebnis(
    IReadOnlyList<Spiel> Plan,
    Planguete? Guete,
    (IReadOnlyList<Spiel> Plan, Planguete Guete)? Grundplan,
    IReadOnlyList<Kriteriumsprobe> Proben,
    IReadOnlyList<Schnellkandidat> Kandidaten,
    double? ErsterGueltigerPlan,
    double? AUndBFertig,
    IReadOnlyList<string> Protokoll,
    IReadOnlyList<Grenzplan> Grenze,
    Massstab Massstab,
    int Stufe = 1,
    IReadOnlyList<Vorschlag>? Vorschlaege = null,
    IReadOnlyList<Kriteriumsstatus>? Status = null,
    int? SchrankeA = null,
    int? SchrankeB = null);
