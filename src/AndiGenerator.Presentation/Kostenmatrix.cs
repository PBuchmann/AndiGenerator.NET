// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Die Kostenmatrix der Kostenansicht: Kostenarten als Spalten, Mannschaften als Zeilen, beide nach Kosten sortiert.</summary>
/// <param name="Spalten">Die angezeigten Kostenarten.</param>
/// <param name="Zeilen">Die Mannschaften.</param>
/// <param name="Verteilung">Anteile der Kostenarten mit Kosten, größter zuerst.</param>
/// <param name="Gesamt">Summe der Mannschaftskosten.</param>
/// <param name="Ausgeblendet">Zahl der Kostenarten ohne Kosten, die ausgeblendet sind.</param>
/// <param name="OhneKosten">Zahl der Kostenarten ohne Kosten insgesamt.</param>
public sealed record Kostenmatrix(
    IReadOnlyList<Matrixspalte> Spalten, IReadOnlyList<Matrixzeile> Zeilen, IReadOnlyList<Kostenanteil> Verteilung, string Gesamt, int Ausgeblendet, int OhneKosten)
{
    /// <summary>Holt die leere Matrix.</summary>
    public static Kostenmatrix Leer { get; } = new([], [], [], string.Empty, 0, 0);
}
