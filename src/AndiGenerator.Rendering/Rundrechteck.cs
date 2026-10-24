// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Ein Rechteck mit abgerundeten Ecken als Vieleck (die Zeichenfläche kennt keine runden Ecken).</summary>
public static class Rundrechteck
{
    private const int Schritte = 5;

    /// <summary>Holt die Eckpunkte eines Rechtecks mit abgerundeten Ecken im Uhrzeigersinn.</summary>
    /// <param name="links">Linke Kante.</param>
    /// <param name="oben">Oberkante.</param>
    /// <param name="rechts">Rechte Kante.</param>
    /// <param name="unten">Unterkante.</param>
    /// <param name="radius">Radius der Ecken (höchstens die halbe Breite bzw. Höhe).</param>
    /// <returns>Die Punkte.</returns>
    public static IReadOnlyList<(double X, double Y)> Punkte(double links, double oben, double rechts, double unten, double radius)
    {
        double r = Math.Max(0, Math.Min(radius, Math.Min(rechts - links, unten - oben) / 2));
        var punkte = new List<(double X, double Y)>();
        (double X, double Y, double Winkel)[] ecken =
        [
            (rechts - r, oben + r, -90),
            (rechts - r, unten - r, 0),
            (links + r, unten - r, 90),
            (links + r, oben + r, 180),
        ];
        foreach ((double mx, double my, double winkel) in ecken)
        {
            for (int i = 0; i <= Schritte; i++)
            {
                double w = (winkel + (90.0 * i / Schritte)) * Math.PI / 180;
                punkte.Add((mx + (r * Math.Cos(w)), my + (r * Math.Sin(w))));
            }
        }

        return punkte;
    }
}
