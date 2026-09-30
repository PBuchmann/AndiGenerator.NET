// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Gefülltes Rechteck.</summary>
/// <param name="X">Linke Kante.</param>
/// <param name="Y">Oberkante.</param>
/// <param name="Breite">Breite.</param>
/// <param name="Hoehe">Höhe.</param>
/// <param name="Farbe">Füllfarbe.</param>
public sealed record RechteckElement(double X, double Y, double Breite, double Hoehe, Farbe Farbe) : IDruckelement
{
    /// <inheritdoc/>
    public void Zeichnen(IZeichenflaeche flaeche)
    {
        ArgumentNullException.ThrowIfNull(flaeche);
        flaeche.Rechteck(X, Y, Breite, Hoehe, Farbe);
    }
}
