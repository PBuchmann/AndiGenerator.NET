// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Linie zwischen zwei Spielen gegen denselben Gegner (Original <c>PaintSpielAbstandMannschaft</c>).</summary>
/// <param name="Von">Termin des ersten Spiels.</param>
/// <param name="Bis">Termin des nächsten Spiels gegen denselben Gegner.</param>
/// <param name="Rot">Rotanteil der Linienfarbe (grün = idealer Abstand, rot = schlecht).</param>
/// <param name="Gruen">Grünanteil der Linienfarbe.</param>
public sealed record Abstandslinie(DateTime Von, DateTime Bis, int Rot, int Gruen);
