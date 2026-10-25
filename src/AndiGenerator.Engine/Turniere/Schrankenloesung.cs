// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ergebnis eines <see cref="ISchrankenloeser"/>.</summary>
/// <param name="Werte">Werte der Spalten der besten gefundenen Lösung, oder <c>null</c>.</param>
/// <param name="Wert">Kosten der besten gefundenen Lösung (<see cref="double.PositiveInfinity"/> ohne Lösung).</param>
/// <param name="Schranke">Beweisbar kleinstmögliche Kosten (untere Schranke).</param>
/// <param name="Optimal">Die Lösung ist beweisbar die beste.</param>
/// <param name="Unloesbar">Das Modell hat keine Lösung.</param>
public sealed record Schrankenloesung(double[]? Werte, double Wert, double Schranke, bool Optimal, bool Unloesbar);
