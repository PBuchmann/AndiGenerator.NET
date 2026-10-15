// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Anzahlen für Kriterien, die im Original nur Kosten haben (nicht im Original): Lücken der Spielverteilung je Mannschaft,
/// zu lange und überlappende Spieltage.
/// </summary>
/// <param name="Spielverteilung">Je Mannschaft (in der Reihenfolge der Bewertung) die zu langen Pausen.</param>
/// <param name="SpieltageLaenge">Spieltage, die länger als eine Woche dauern (auf 10 Mannschaften bezogen).</param>
/// <param name="SpieltageUeberlappung">Spieltage, die beginnen, bevor ein früherer zu Ende ist.</param>
/// <param name="LetzterLaenge">1, wenn der letzte Spieltag zu lang ist.</param>
/// <param name="LetzterUeberlappung">1, wenn der letzte Spieltag überlappt.</param>
/// <param name="Spieltagmeldungen">Meldungen zu den Spieltagen (leer, wenn nicht angefordert).</param>
/// <param name="Verteilungsmeldungen">Je Mannschaft die Meldungen zu ihren Pausen (leer, wenn nicht angefordert).</param>
/// <param name="LetzterMeldungen">Die Meldungen zum letzten Spieltag (leer, wenn nicht angefordert).</param>
public sealed record Zusatzverstoesse(
    IReadOnlyList<int> Spielverteilung,
    int SpieltageLaenge,
    int SpieltageUeberlappung,
    int LetzterLaenge,
    int LetzterUeberlappung,
    IReadOnlyList<string> Spieltagmeldungen,
    IReadOnlyList<IReadOnlyList<string>> Verteilungsmeldungen,
    IReadOnlyList<string> LetzterMeldungen);
