// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Zwischenstand eines Laufs des <see cref="IGanzzahlloeser"/> (nicht im Original).</summary>
/// <param name="Wert">Kosten der besten bisher gefundenen Lösung (<see cref="double.PositiveInfinity"/>, solange es keine gibt).</param>
/// <param name="Schranke">Beweisbar kleinstmögliche Kosten (untere Schranke) nach dem bisherigen Stand der Suche.</param>
public sealed record Loeserstand(double Wert, double Schranke);
