// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

/// <summary>Eine Mannschaft der Staffel.</summary>
/// <param name="KeineDoppelspieltage">Attribut <c>noDoubleDays</c>, Standard <c>true</c>.</param>
/// <param name="KeinWochenspielGegen">60-km-Regel: Gegner, gegen die nicht unter der Woche gespielt wird.</param>
public sealed record ClickTtMannschaft(
    string Id,
    string VereinsId,
    int Nummer,
    string Geschlecht,
    string Name,
    bool KeineDoppelspieltage,
    IReadOnlyList<ClickTtHeimspieltermin> Heimspieltermine,
    IReadOnlyList<ClickTtZeitraum> Sperrzeitraeume,
    IReadOnlyList<ClickTtAuswaertskoppel> Auswaertskoppeln,
    IReadOnlyList<string> KeinWochenspielGegen,
    IReadOnlyList<ClickTtNachbarmannschaft> Nachbarmannschaften);
