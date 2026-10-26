// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ergebnis eines Laufs des Kompaktplaners.</summary>
/// <param name="Varianten">Die Vorschläge, der ausgewogene zuerst; leer, wenn kein vollständiger Plan gefunden wurde.</param>
/// <param name="Geprueft">Zahl der Suchversuche.</param>
/// <param name="Hinweise">Hinweise zu Eingaben und Ergebnis.</param>
public sealed record Kompaktergebnis(IReadOnlyList<Kompaktvariante> Varianten, long Geprueft, IReadOnlyList<string> Hinweise)
{
    /// <summary>
    /// Holt je Schritt der gezielten Konstruktion, welche Qualität gefunden wurde und was nach dem Beweis von HiGHS mindestens
    /// nötig ist, z. B. „Kürzester Plan: 2.315 km, … – nicht bewiesen bestmöglich – mindestens ≈ 2.288 km sind nötig“.
    /// </summary>
    public IReadOnlyList<string> Qualitaet { get; init; } = [];
}
