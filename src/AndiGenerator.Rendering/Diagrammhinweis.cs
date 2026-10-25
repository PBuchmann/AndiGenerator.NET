// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Eine Stelle im Diagramm, zu der die Ansicht beim Überfahren mit der Maus einen Text zeigt: ein Kreis (Radius) oder – mit
/// Breite und Höhe – ein Rechteck um den Mittelpunkt; dazu auf Wunsch hervorgehobene verbundene Stellen.
/// </summary>
/// <param name="X">Mittelpunkt x.</param>
/// <param name="Y">Mittelpunkt y.</param>
/// <param name="Radius">Radius des Kreises (ohne Breite und Höhe).</param>
/// <param name="Text">Der Text, mehrzeilig.</param>
/// <param name="Breite">Breite des Rechtecks; 0 = Kreis.</param>
/// <param name="Hoehe">Höhe des Rechtecks.</param>
/// <param name="Marken">Stellen, die beim Überfahren hervorgehoben werden.</param>
public sealed record Diagrammhinweis(
    double X,
    double Y,
    double Radius,
    string Text,
    double Breite = 0,
    double Hoehe = 0,
    IReadOnlyList<Diagrammmarke>? Marken = null)
{
    /// <summary>Holt die linke Kante der Stelle.</summary>
    public double Links => Breite > 0 ? X - (Breite / 2) : X - Radius;

    /// <summary>Holt die rechte Kante der Stelle.</summary>
    public double Rechts => Breite > 0 ? X + (Breite / 2) : X + Radius;

    /// <summary>Holt die Oberkante der Stelle.</summary>
    public double Oben => Breite > 0 ? Y - (Hoehe / 2) : Y - Radius;

    /// <summary>Holt die Unterkante der Stelle.</summary>
    public double Unten => Breite > 0 ? Y + (Hoehe / 2) : Y + Radius;

    /// <summary>Prüft, ob ein Punkt auf der Stelle liegt (beim Kreis mit etwas Spielraum).</summary>
    /// <param name="x">Punkt x.</param>
    /// <param name="y">Punkt y.</param>
    /// <returns><c>true</c>, wenn ja.</returns>
    public bool Trifft(double x, double y) => Breite > 0
        ? x >= Links && x <= Rechts && y >= Oben && y <= Unten
        : ((x - X) * (x - X)) + ((y - Y) * (y - Y)) <= (Radius + 2) * (Radius + 2);
}
