// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Engine.Turniere;

/// <summary>Eingaben des Kompaktplaners (nicht im Original).</summary>
/// <param name="Mannschaften">Alle Mannschaften der Staffel.</param>
/// <param name="Tage">Mögliche Spieltage (Sonntage der Halbrunde).</param>
/// <param name="Meldungen">Meldungen der Mannschaften; ohne Meldung gilt jeder Tag als möglich.</param>
/// <param name="Entfernung">Einfache Entfernung zwischen den Hallen zweier Mannschaften in km (Luftlinie).</param>
public sealed record Kompaktaufgabe(
    IReadOnlyList<string> Mannschaften,
    IReadOnlyList<DateOnly> Tage,
    IReadOnlyList<Turniermeldung> Meldungen,
    Func<string, string, double> Entfernung)
{
    /// <summary>
    /// Holt die vier stärksten Mannschaften (nach QTTR-Schnitt, die stärkste zuerst) für einen Spitzenspieltag oder leer.
    /// Zusätzlich wird der kürzeste Plan gesucht, in dem sie einen 4er-Spieltag bilden (die beiden ersten spielen
    /// gegeneinander) und die vier schwächsten ebenso – so spät wie möglich.
    /// </summary>
    public IReadOnlyList<string> Spitze { get; init; } = [];

    /// <summary>Holt die vier schwächsten Mannschaften (die schwächste zuerst) für einen Abstiegsspieltag oder leer.</summary>
    public IReadOnlyList<string> Keller { get; init; } = [];

    /// <summary>Holt Hinweise zur Aufgabe (z. B. fehlende QTTR-Werte), die mit dem Ergebnis gezeigt werden.</summary>
    public IReadOnlyList<string> Hinweise { get; init; } = [];
}
