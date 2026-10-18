// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Leitstand der Optimierung: was die Rechenkerne und Inseln gerade tun (Anzeige „Leitstand“).
/// </summary>
/// <param name="Kerne">Berechnete Pläne je Rechen-Thread seit dem Start (ohne Rechen-Threads leer).</param>
/// <param name="Inseln">Die Inseln, zuletzt die Spezial-Insel, wenn es sie gibt.</param>
/// <param name="DurchlaeufeVorNeustart">Pläne ohne Verbesserung, nach denen die schlechteste Insel neu startet.</param>
/// <param name="SpezialKostenart">Kostenart, die die Spezial-Insel gerade extrem gewichtet; <c>null</c>, solange sie nicht läuft.</param>
/// <param name="SpezialVerwendet">Wie oft die Lösung der Spezial-Insel eine Insel neu gestartet hat.</param>
/// <param name="BesterPlan">Die Kriterien des besten Plans (Kosten bzw. im Automodus Verstöße je Kriterium).</param>
/// <param name="BesterPlanNummer">Zählt weiter, sobald sich der geltende beste Plan ändert (zusammen mit <paramref name="BesterPlan"/> ermittelt).</param>
/// <param name="Starts">Im Automodus je Startverfahren die Neustarts der Inseln und ihre Erfolge; leer, solange keine.</param>
public sealed record Maschinenraum(
    IReadOnlyList<long> Kerne,
    IReadOnlyList<Inselstand> Inseln,
    long DurchlaeufeVorNeustart,
    MannschaftsKostenart? SpezialKostenart,
    int SpezialVerwendet,
    IReadOnlyList<Kriterienwert>? BesterPlan = null,
    int BesterPlanNummer = 0,
    IReadOnlyList<Startstatistik>? Starts = null);
