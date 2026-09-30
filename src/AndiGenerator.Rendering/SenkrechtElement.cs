// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Senkrechter Text von unten nach oben (Spaltenköpfe der Kostenmatrix).</summary>
/// <param name="Text">Der Text.</param>
/// <param name="X">Linke Kante der Zeichen.</param>
/// <param name="Y">Unterkante: hier beginnt der Text.</param>
/// <param name="Groesse">Schriftgröße in Punkt.</param>
/// <param name="Farbe">Schriftfarbe.</param>
public sealed record SenkrechtElement(string Text, double X, double Y, double Groesse, Farbe Farbe) : IDruckelement
{
    /// <inheritdoc/>
    public void Zeichnen(IZeichenflaeche flaeche)
    {
        ArgumentNullException.ThrowIfNull(flaeche);
        flaeche.TextSenkrecht(Text, X, Y, Groesse, Farbe);
    }
}
