// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Linie.</summary>
/// <param name="X1">Anfang x.</param>
/// <param name="Y1">Anfang y.</param>
/// <param name="X2">Ende x.</param>
/// <param name="Y2">Ende y.</param>
/// <param name="Farbe">Farbe.</param>
/// <param name="Staerke">Linienstärke.</param>
public sealed record LinienElement(double X1, double Y1, double X2, double Y2, Farbe Farbe, double Staerke) : IDruckelement
{
    /// <inheritdoc/>
    public void Zeichnen(IZeichenflaeche flaeche)
    {
        ArgumentNullException.ThrowIfNull(flaeche);
        flaeche.Linie(X1, Y1, X2, Y2, Farbe, Staerke);
    }
}
