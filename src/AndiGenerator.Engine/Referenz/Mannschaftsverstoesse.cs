// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Die Verstöße einer Mannschaft, wie sie die Bewertung (Qualitätsansicht) zählt, für die Diagramme; <c>null</c>, wenn die
/// Bewertung das Kriterium nicht berücksichtigt.
/// </summary>
/// <param name="Wechsel">Kein Wechsel Heim/Auswärts (Spiele, die nicht wechseln; Koppeltermine zählen nicht).</param>
/// <param name="EngeTermine">Spiele höchstens drei Tage nach dem vorigen (außer Koppeltermine).</param>
/// <param name="ZweiSpieleProWoche">Spiele in derselben Woche wie das vorige (außer Koppeltermine).</param>
/// <param name="Pausen">Zu lange Pausen der Spielverteilung.</param>
/// <param name="AbstandHinRueck">Größte Abweichung des Abstands Hin-/Rückspiel vom Mittel (in Spielen).</param>
/// <param name="Setzliste">Spiele gegen einen für die Stelle in der Runde zu weit entfernten Gegner.</param>
/// <param name="Pausenzeiten">Beginn und Ende der zu langen Pausen.</param>
public sealed record Mannschaftsverstoesse(
    int? Wechsel,
    int? EngeTermine,
    int? ZweiSpieleProWoche,
    int? Pausen,
    int? AbstandHinRueck,
    int? Setzliste,
    IReadOnlyList<(DateTime Von, DateTime Bis)> Pausenzeiten);
