// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Punkt im Verlauf der Suche (Maschinenraum).</summary>
/// <param name="Sekunden">Rechenzeit seit dem Start (ohne Pausen).</param>
/// <param name="Kosten">Kosten des besten Plans; negativ = noch keiner.</param>
/// <param name="A">Im Automodus nach der Basisoptimierung: Verstöße der Stufe A des besten Plans; sonst <c>null</c>.</param>
/// <param name="B">Ebenso Stufe B.</param>
/// <param name="C">Ebenso Stufe C.</param>
/// <param name="PlaeneProSekunde">Berechnete Pläne je Sekunde.</param>
/// <param name="Phase">Phase des Automodus („Basisoptimierung“, „Optimierung Stufe A“ …); leer bei der Kostenoptimierung.</param>
/// <param name="Anhebungen">Wie oft der Automodus bis hierher Gewichte angehoben hat.</param>
public sealed record Verlaufspunkt(double Sekunden, double Kosten, int? A, int? B, int? C, double PlaeneProSekunde, string Phase, int Anhebungen);
