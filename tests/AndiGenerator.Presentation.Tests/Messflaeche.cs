// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Zeichenfläche mit fester Zeichenbreite (halbe Schriftgröße, fett 0,6): misst und schreibt Aufrufe mit.</summary>
internal sealed class Messflaeche : IZeichenflaeche
{
    /// <summary>Holt die gezeichneten Texte.</summary>
    public List<string> Texte { get; } = [];

    /// <summary>Holt Art, Position und Größe der Texte (auch senkrechter) und Kreise.</summary>
    public List<(string Art, double X, double Y, double Groesse)> Punkte { get; } = [];

    /// <summary>Holt die Anzahl der gezeichneten Linien.</summary>
    public int Linien { get; private set; }

    /// <summary>Holt die Anzahl der gezeichneten Rechtecke.</summary>
    public int Rechtecke { get; private set; }

    public double TextBreite(string text, double groesse, bool fett) => text.Length * groesse * (fett ? 0.6 : 0.5);

    public void Text(string text, double x, double y, double groesse, Farbe farbe, bool fett)
    {
        Texte.Add(text);
        Punkte.Add(("Text", x, y, groesse));
    }

    public void TextSenkrecht(string text, double x, double y, double groesse, Farbe farbe)
    {
        Texte.Add(text);
        Punkte.Add(("Senkrecht", x, y, groesse));
    }

    public void Linie(double x1, double y1, double x2, double y2, Farbe farbe, double staerke) => Linien++;

    public void Rechteck(double x, double y, double breite, double hoehe, Farbe farbe) => Rechtecke++;

    public void Kreis(double mx, double my, double radius, Farbe fuellung, Farbe rand, double staerke) => Punkte.Add(("Kreis", mx, my, radius));
}
