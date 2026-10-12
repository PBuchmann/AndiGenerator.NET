// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Application;

/// <summary>Ein Kompaktspieltag im Plan (nicht im Original).</summary>
/// <param name="Tag">Datum.</param>
/// <param name="Form">3er, 4er oder Einzelbegegnung.</param>
/// <param name="Ausrichter">Ausrichtende Mannschaft (bei der Einzelbegegnung die Heimmannschaft) oder <c>null</c>, wenn keine passt.</param>
/// <param name="Mannschaften">Alle beteiligten Mannschaften.</param>
/// <param name="Spiele">Die Spiele, nach Uhrzeit.</param>
/// <param name="HalleVerlegen">
/// Spiele, deren Heimmannschaft nicht der Ausrichter ist: In click-TT wird bei ihnen die Halle des Ausrichters eingetragen.
/// </param>
/// <param name="Hinweise">Verstöße gegen die Meldungen und andere Befunde.</param>
/// <param name="Ausweichtage">Andere Sonntage, an denen der Spieltag zu den Meldungen passen würde.</param>
/// <param name="Km">Kilometer Luftlinie aller Gäste, hin und zurück.</param>
public sealed record Kompaktspieltag(
    DateOnly Tag,
    Turnierform Form,
    string? Ausrichter,
    IReadOnlyList<string> Mannschaften,
    IReadOnlyList<Spiel> Spiele,
    IReadOnlyList<Spiel> HalleVerlegen,
    IReadOnlyList<string> Hinweise,
    IReadOnlyList<DateOnly> Ausweichtage,
    double Km)
{
    /// <summary>Holt die Gäste (alle außer dem Ausrichter).</summary>
    public IReadOnlyList<string> Gaeste { get; } = Mannschaften.Where(m => m != Ausrichter).ToList();
}
