// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Text an einer festen Stelle (Oberkante).</summary>
/// <param name="Text">Der Text.</param>
/// <param name="X">Linke Kante.</param>
/// <param name="Y">Oberkante.</param>
/// <param name="Groesse">Schriftgröße.</param>
/// <param name="Farbe">Schriftfarbe.</param>
/// <param name="Fett">Fettschrift.</param>
public sealed record TextElement(string Text, double X, double Y, double Groesse, Farbe Farbe, bool Fett) : IDruckelement
{
    /// <inheritdoc/>
    public void Zeichnen(IZeichenflaeche flaeche)
    {
        ArgumentNullException.ThrowIfNull(flaeche);
        flaeche.Text(Text, X, Y, Groesse, Farbe, Fett);
    }
}
