// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Geo;

/// <summary>Lage auf der Erde in Grad (WGS 84).</summary>
/// <param name="Breite">Geographische Breite.</param>
/// <param name="Laenge">Geographische Länge.</param>
public readonly record struct Koordinate(double Breite, double Laenge)
{
    private const double Erdradius = 6371.0;

    /// <summary>Entfernung in Luftlinie (Großkreis).</summary>
    /// <param name="ziel">Der andere Ort.</param>
    /// <returns>Die Entfernung in Kilometern.</returns>
    public double Luftlinie(Koordinate ziel)
    {
        double b1 = Bogen(Breite);
        double b2 = Bogen(ziel.Breite);
        double db = b2 - b1;
        double dl = Bogen(ziel.Laenge - Laenge);
        double h = (Math.Sin(db / 2) * Math.Sin(db / 2)) + (Math.Cos(b1) * Math.Cos(b2) * Math.Sin(dl / 2) * Math.Sin(dl / 2));
        return 2 * Erdradius * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
    }

    private static double Bogen(double grad) => grad * Math.PI / 180.0;
}
