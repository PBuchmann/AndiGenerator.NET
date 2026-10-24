// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Eine Stelle, die beim Überfahren eines <see cref="Diagrammhinweis"/> hervorgehoben wird: mit einem Ring oder – mit Breite
/// und Höhe – einem Rahmen mit runden Ecken um den Mittelpunkt.
/// </summary>
/// <param name="X">Mittelpunkt x.</param>
/// <param name="Y">Mittelpunkt y.</param>
/// <param name="Radius">Radius des Rings bzw. der Ecken des Rahmens.</param>
/// <param name="Breite">Breite des Rahmens; 0 = Ring.</param>
/// <param name="Hoehe">Höhe des Rahmens.</param>
public sealed record Diagrammmarke(double X, double Y, double Radius, double Breite = 0, double Hoehe = 0)
{
    /// <summary>Holt die halbe Ausdehnung waagerecht.</summary>
    public double HalbeBreite => Breite > 0 ? Breite / 2 : Radius;
}
