// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Messung der Prozessorlast für den Verlauf im Leitstand.</summary>
/// <param name="Zeitpunkt">Zeitstempel (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>).</param>
/// <param name="Suche">Anteil der Suche am ganzen Prozessor (0 bis 1).</param>
/// <param name="Highs">Anteil von HiGHS.</param>
/// <param name="Rest">Anteil des übrigen Programms.</param>
public sealed record Lastpunkt(long Zeitpunkt, double Suche, double Highs, double Rest);
