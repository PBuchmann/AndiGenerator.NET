// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Spielplandaten der Staffel im Überblick.</summary>
/// <param name="Mannschaften">Je Mannschaft: Heimtermine und Sperrtermine (Anzahl).</param>
/// <param name="SpielfreieTage">Tage, an denen niemand spielt.</param>
/// <param name="Pflichtspielzeiten">Zeiträume mit Mindestzahl an Spielen (Nummer, Zeitraum, Anzahl).</param>
/// <param name="FestgelegteBegegnungen">Manuell festgelegte Begegnungen (Nummer, Termin, Heim – Gast).</param>
/// <param name="Setzliste">Setzliste: aktiv und Reihenfolge.</param>
public sealed record Spielplandaten(
    IReadOnlyList<string> Mannschaften,
    IReadOnlyList<string> SpielfreieTage,
    IReadOnlyList<string> Pflichtspielzeiten,
    IReadOnlyList<string> FestgelegteBegegnungen,
    string Setzliste);
