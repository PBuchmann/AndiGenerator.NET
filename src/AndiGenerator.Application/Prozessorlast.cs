// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>
/// Wie sich der ganze Prozessor gerade aufteilt (nicht im Original; für den Leitstand): Anteile von 0 bis 1 an der Rechenzeit
/// aller Kerne seit der letzten Messung.
/// </summary>
/// <param name="Suche">Die Fäden der Suche (Inseln).</param>
/// <param name="Highs">HiGHS (untere Schranke, Teilneuplanung).</param>
/// <param name="Sonstiges">Der Rest des Programms (Oberfläche, Koordinator, Speicherbereinigung).</param>
/// <param name="Prozessoren">Logische Prozessoren (Kerne) des Rechners.</param>
public sealed record Prozessorlast(double Suche, double Highs, double Sonstiges, int Prozessoren)
{
    /// <summary>Holt, wie viele Kerne HiGHS gerade auslastet (1 = ein Kern voll).</summary>
    public double HighsKerne => Highs * Prozessoren;

    /// <summary>Holt den freien Anteil (andere Programme oder Leerlauf).</summary>
    public double Frei => Math.Max(0, 1 - Suche - Highs - Sonstiges);
}
