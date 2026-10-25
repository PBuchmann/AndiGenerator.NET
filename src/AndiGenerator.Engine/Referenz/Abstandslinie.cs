// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Abstand zwischen zwei Spielen gegen denselben Gegner (Original <c>PaintSpielAbstandMannschaft</c>).</summary>
/// <param name="Gegner">Name des Gegners.</param>
/// <param name="Von">Termin des ersten Spiels.</param>
/// <param name="Bis">Termin des nächsten Spiels gegen denselben Gegner.</param>
/// <param name="Abstand">Abstand in Spielen der Mannschaft (1 = gleich das nächste Spiel).</param>
/// <param name="Ideal">Idealer Abstand: Spiele der Mannschaft je Runde.</param>
public sealed record Abstandslinie(string Gegner, DateTime Von, DateTime Bis, int Abstand, double Ideal);
