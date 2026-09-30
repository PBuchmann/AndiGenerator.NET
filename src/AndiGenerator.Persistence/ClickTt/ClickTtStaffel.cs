// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.ClickTt;

// Typisiertes, originalgetreues Abbild der click-TT-Exportdatei (Wurzel <TT>, Schema TTGenerator.xsd).
// Alle Texte sind wie im Original getrimmt (Befund #32). Fachliche Ableitungen (Doppelspieltage,
// Koppel-Zweitzeiten, Pflichtspielzeiträume …) passieren NICHT hier, sondern beim Übergang ins Fachmodell.

/// <summary>Eine Staffel aus dem click-TT-Export.</summary>
/// <param name="Rueckrundenbeginn">Attribut <c>mid</c>; im Original <c>DateBeginRueckrundeInXML</c>.</param>
public sealed record ClickTtStaffel(
    string Name,
    string Id,
    string Geschlecht,
    DateOnly Von,
    DateOnly Bis,
    DateOnly? Rueckrundenbeginn,
    string? SaisonTyp,
    IReadOnlyList<ClickTtMannschaft> Mannschaften,
    IReadOnlyList<ClickTtSpiel> VorgegebeneSpiele,
    IReadOnlyList<ClickTtZeitraum> SpielfreieZeitraeume,
    IReadOnlyList<DateOnly> Pflichtspieltage,
    IReadOnlyList<ClickTtSpiel> BestehenderSpielplan);
