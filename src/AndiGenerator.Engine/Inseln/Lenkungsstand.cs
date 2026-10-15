// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Inseln;

/// <summary>Stand des Automodus (MIGRATIONSPLAN Abschnitt 11).</summary>
/// <param name="Basis">Bewertung des besten Plans am Ende des Grundlaufs; <c>null</c>, solange der Grundlauf läuft.</param>
/// <param name="Bester">Bewertung des besten Plans nach Stufen.</param>
/// <param name="Suche">Bewertung des Plans, an dem die Suche gerade arbeitet (bester nach den aktuellen Gewichten).</param>
/// <param name="Phase">Grundlauf, Drängen oder Glätten (mit dem aktuellen C-Kriterium), für Protokolle.</param>
/// <param name="Bezeichnung">Phase für die Oberfläche: „Basisoptimierung“, „Optimierung Stufe A“, „… Stufe B“ oder „… Stufe C“.</param>
/// <param name="Anhebungen">Wie oft Gewichte angehoben wurden.</param>
/// <param name="Zurueckgezogen">Wie oft das Glätten eines C-Kriteriums zurückgezogen wurde, weil A oder B stiegen.</param>
/// <param name="Gewichtungen">Die aktuell geltenden Gewichtungen (vom Automodus angepasst).</param>
/// <param name="Aenderungen">Die Änderungen mit Zeitpunkt, z. B. „42 s angehoben: Hallenbelegung TSV A 1 Hoch“.</param>
/// <param name="Kriterien">
/// Die Kriterien, an denen gerade gearbeitet wird, in der Reihenfolge der Einteilung: in Stufe A und B die zuletzt
/// angehobenen, in Stufe C das, das geglättet wird; leer in der Basisoptimierung.
/// </param>
/// <param name="Gewichte">Die Gewichte, die der Automodus gegenüber den Gewichtungen des Anwenders geändert hat.</param>
/// <param name="SchrankeA">Die untere Schranke aus HiGHS für die Summe der Stufe A; <c>null</c>, solange keine bekannt ist.</param>
/// <param name="SchrankeB">Die untere Schranke für die Summe der Stufe B bei bestmöglichem A; <c>null</c>, solange keine bekannt ist.</param>
/// <param name="VonInseln">So oft ist der Plan einer Insel bester Plan geworden (nach der Basisoptimierung).</param>
/// <param name="VonHighs">So oft ist ein Plan aus HiGHS bester Plan geworden.</param>
/// <param name="ZuletztVon">Woher der beste Plan zuletzt kam, z. B. „Insel 3“ oder „HiGHS“; <c>null</c>, solange keiner.</param>
/// <param name="Festgelegt">Was der Staffelleiter festgelegt hat (A und B bleiben so), oder <c>null</c>.</param>
public sealed record Lenkungsstand(
    Stufenwert? Basis,
    Stufenwert Bester,
    Stufenwert? Suche,
    string Phase,
    string Bezeichnung,
    int Anhebungen,
    int Zurueckgezogen,
    Berechnungsoptionen Gewichtungen,
    IReadOnlyList<string> Aenderungen,
    IReadOnlyList<Kostenkriterium>? Kriterien = null,
    IReadOnlyList<Gewichtsanhebung>? Gewichte = null,
    int? SchrankeA = null,
    int? SchrankeB = null,
    int VonInseln = 0,
    int VonHighs = 0,
    string? ZuletztVon = null,
    string? Festgelegt = null);
