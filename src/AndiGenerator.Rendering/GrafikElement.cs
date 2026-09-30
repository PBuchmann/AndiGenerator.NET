// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Platzierte Grafik: versetzt und verkleinert gezeichnet.</summary>
/// <param name="X">Linke Kante.</param>
/// <param name="Y">Oberkante.</param>
/// <param name="Skala">Verkleinerung (1 = Originalgröße).</param>
/// <param name="Zeichnung">Zeichnet die Grafik ab (0, 0).</param>
public sealed record GrafikElement(double X, double Y, double Skala, Action<IZeichenflaeche> Zeichnung) : IDruckelement
{
    /// <inheritdoc/>
    public void Zeichnen(IZeichenflaeche flaeche)
    {
        ArgumentNullException.ThrowIfNull(flaeche);
        Zeichnung(new VersetzteZeichenflaeche(flaeche, X, Y, Skala));
    }
}
