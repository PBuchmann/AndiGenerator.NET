// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Punkt im Verlauf der Suche (im Takt der Statusabfrage).</summary>
/// <param name="Sekunden">Rechenzeit seit dem Start (ohne Pausen).</param>
/// <param name="Kosten">Kosten des besten Plans; negativ = noch keiner.</param>
/// <param name="A">Im Automodus: Verstöße der Stufe A des besten Plans (in der Basisoptimierung des besten nach Kosten); sonst <c>null</c>.</param>
/// <param name="B">Ebenso Stufe B.</param>
/// <param name="C">Ebenso Stufe C.</param>
/// <param name="PlaeneProSekunde">Berechnete Pläne je Sekunde.</param>
/// <param name="Phase">Phase des Automodus („Basisoptimierung“, „Optimierung Stufe A“ …); leer bei der Kostenoptimierung.</param>
/// <param name="Anhebungen">Wie oft der Automodus bis hierher Gewichte angehoben hat.</param>
/// <param name="SchrankeA">Die untere Grenze aus HiGHS für die Stufe A; <c>null</c>, solange keine bekannt ist.</param>
/// <param name="SchrankeB">Ebenso für die Stufe B (bei bestmöglichem A).</param>
/// <param name="Suche">Anteil des Prozessors für die Suche der Inseln (0 bis 1); 0, wenn nicht gemessen.</param>
/// <param name="Highs">Anteil für HiGHS (untere Schranke, Teilneuplanung).</param>
/// <param name="Rest">Anteil für den Rest des Programms und andere Programme.</param>
public sealed record Verlaufspunkt(
    double Sekunden,
    double Kosten,
    int? A,
    int? B,
    int? C,
    double PlaeneProSekunde,
    string Phase,
    int Anhebungen,
    int? SchrankeA = null,
    int? SchrankeB = null,
    double Suche = 0,
    double Highs = 0,
    double Rest = 0);
