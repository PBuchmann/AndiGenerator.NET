// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Anzahlen für Kriterien, die im Original nur Kosten haben (nicht im Original): Lücken der Spielverteilung je Mannschaft,
/// zu lange und überlappende Spieltage; dazu der Zuschlag, mit dem der Wechsel H/A steigend zählt.
/// </summary>
/// <param name="Spielverteilung">Je Mannschaft (in der Reihenfolge der Bewertung) die zu langen Pausen.</param>
/// <param name="SpieltageLaenge">Spieltage, die länger als eine Woche dauern (auf 10 Mannschaften bezogen).</param>
/// <param name="SpieltageUeberlappung">Spieltage, die beginnen, bevor ein früherer zu Ende ist.</param>
/// <param name="LetzterLaenge">1, wenn der letzte Spieltag zu lang ist.</param>
/// <param name="LetzterUeberlappung">1, wenn der letzte Spieltag überlappt.</param>
/// <param name="Spieltagmeldungen">Meldungen zu den Spieltagen (leer, wenn nicht angefordert).</param>
/// <param name="Verteilungsmeldungen">Je Mannschaft die Meldungen zu ihren Pausen (leer, wenn nicht angefordert).</param>
/// <param name="LetzterMeldungen">Die Meldungen zum letzten Spieltag (leer, wenn nicht angefordert).</param>
/// <param name="Pausen">Je Mannschaft Beginn und Ende ihrer zu langen Pausen (Delphi-Datum); <c>null</c> = nicht ermittelt.</param>
/// <param name="ZuLangeSpieltage">Die zu langen Spieltage (0 = erster); <c>null</c> = nicht ermittelt.</param>
/// <param name="UeberlappendeSpieltage">Die überlappenden Spieltage (0 = erster); <c>null</c> = nicht ermittelt.</param>
/// <param name="Wechselzuschlag">
/// Je Mannschaft der Zuschlag zum Wechsel H/A, der damit steigend zählt (Peter, 29.10.2026: mehrfach hintereinander zu Hause
/// fällt mehr auf): Das Original zählt jede Folge gleicher Art einmal (drei Heimspiele in Folge 2), steigend zählt das zweite
/// 1, das dritte 2 … (drei in Folge 1 + 2 = 3, vier 6); der Zuschlag ist der Unterschied. Nur Anzahl (Automodus, einfacher
/// Modus, Qualitätsansicht), die Kostentabelle bleibt wie im Original; <c>null</c> = nicht ermittelt.
/// </param>
public sealed record Zusatzverstoesse(
    IReadOnlyList<int> Spielverteilung,
    int SpieltageLaenge,
    int SpieltageUeberlappung,
    int LetzterLaenge,
    int LetzterUeberlappung,
    IReadOnlyList<string> Spieltagmeldungen,
    IReadOnlyList<IReadOnlyList<string>> Verteilungsmeldungen,
    IReadOnlyList<string> LetzterMeldungen,
    IReadOnlyList<IReadOnlyList<(double Start, double Ende)>>? Pausen = null,
    IReadOnlyList<int>? ZuLangeSpieltage = null,
    IReadOnlyList<int>? UeberlappendeSpieltage = null,
    IReadOnlyList<int>? Wechselzuschlag = null);
