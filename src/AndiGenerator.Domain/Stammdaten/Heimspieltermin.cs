// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Heimspielwunsch einer Mannschaft (Original <c>homegameday</c>, gelesen von <c>TMannschaft.loadHomeGame</c>).</summary>
/// <param name="Zeitpunkt">Datum und Uhrzeit.</param>
/// <param name="MaxParalleleSpiele">Höchstzahl gleichzeitiger Heimspiele des Vereins, 0 = keine Angabe (<c>parallelgames</c>).</param>
/// <param name="Ausweichtermin">Nur als Ausweichtermin zu nutzen (<c>ausweichtermin</c>).</param>
/// <param name="Spiellokal">Spiellokal „1“ bis „5“ oder leer = Spiellokal der Mannschaft (<c>location</c>).</param>
/// <param name="Koppelung">Koppeltermin oder Doppelspieltag (<c>couplegameday</c>, <c>doublegameday</c>).</param>
/// <param name="Prioritaet">Verbindlichkeit der Koppelung (<c>coupleprio</c>); ohne Koppelung ohne Bedeutung.</param>
/// <param name="ZweiteUhrzeit">Uhrzeit des zweiten Spiels am selben Tag (<c>couplesecondtime</c>); <c>null</c>, wenn nicht angegeben.</param>
/// <param name="ZweiteUhrzeitFuerAuswaertskoppel">Die zweite Uhrzeit gilt für Auswärtskoppel (<c>coupleauswaertssecondtime</c>).</param>
public sealed record Heimspieltermin(
    DateTime Zeitpunkt,
    int MaxParalleleSpiele,
    bool Ausweichtermin,
    string Spiellokal,
    Terminkoppelung Koppelung,
    Koppelprioritaet Prioritaet,
    TimeOnly? ZweiteUhrzeit,
    bool ZweiteUhrzeitFuerAuswaertskoppel);
