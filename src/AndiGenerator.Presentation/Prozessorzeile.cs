// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Eine Zeile unter der Prozessorleiste im Leitstand: ein Teil des Prozessors mit seinem Anteil als Balken (nur einer der
/// vier Anteile ist gesetzt, er bestimmt die Farbe) und in Kernen.
/// </summary>
/// <param name="Name">Suche, HiGHS, Rest oder frei.</param>
/// <param name="Suche">Anteil der Suche (0 bis 1).</param>
/// <param name="Highs">Anteil von HiGHS.</param>
/// <param name="Rest">Anteil des übrigen Programms.</param>
/// <param name="Frei">Freier Anteil.</param>
/// <param name="Kerne">Der Anteil in Kernen, z. B. <c>10,2 Kerne</c>.</param>
public sealed record Prozessorzeile(string Name, double Suche, double Highs, double Rest, double Frei, string Kerne);
