// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Stand der Teilneuplanung im Automodus (für den Leitstand).</summary>
/// <param name="Zustand">Was sie gerade tut, z. B. „wartet auf das Ende der Basisoptimierung“ oder „plant neu“.</param>
/// <param name="Ausschnitte">Neu geplante Ausschnitte insgesamt.</param>
/// <param name="Besser">Davon echt besser (angenommen).</param>
/// <param name="Eingespielt">So oft ist ihr Plan sofort bester Plan des Automodus geworden.</param>
/// <param name="Wechsel">So oft hat sie den besseren Plan der Inseln als neuen Ausgangsplan übernommen.</param>
/// <param name="Rechnet">Gibt an, ob HiGHS gerade rechnet (nicht wartend oder angehalten).</param>
/// <param name="Mannschaften">Mannschaften des Ausschnitts, den HiGHS gerade neu plant; <c>null</c>, wenn keiner läuft.</param>
/// <param name="Verlauf">Wie die letzten Ausschnitte ausgegangen sind (und wann sie den Plan der Inseln übernommen hat), der älteste zuerst.</param>
/// <param name="Aktuell">Ihr Plan, von dem aus sie gerade neu plant.</param>
/// <param name="Groessen">Erfolg je Größe der Ausschnitte (kleinste zuerst).</param>
public sealed record Teilneuplanungsstand(
    string Zustand,
    int Ausschnitte,
    int Besser,
    int Eingespielt,
    int Wechsel,
    bool Rechnet = false,
    int? Mannschaften = null,
    IReadOnlyList<Ausschnittsergebnis>? Verlauf = null,
    Stufenwert? Aktuell = null,
    IReadOnlyList<Ausschnittsgroesse>? Groessen = null);
