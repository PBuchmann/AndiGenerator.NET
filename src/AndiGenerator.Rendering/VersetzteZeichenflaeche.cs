// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Zeichenfläche, die Koordinaten und Größen verschiebt und skaliert an eine andere weitergibt.</summary>
/// <param name="ziel">Die eigentliche Fläche.</param>
/// <param name="x0">Verschiebung x.</param>
/// <param name="y0">Verschiebung y.</param>
/// <param name="skala">Skalierung.</param>
internal sealed class VersetzteZeichenflaeche(IZeichenflaeche ziel, double x0, double y0, double skala) : IZeichenflaeche
{
    public double TextBreite(string text, double groesse, bool fett) => ziel.TextBreite(text, groesse * skala, fett) / skala;

    public void Text(string text, double x, double y, double groesse, Farbe farbe, bool fett) =>
        ziel.Text(text, X(x), Y(y), groesse * skala, farbe, fett);

    public void TextSenkrecht(string text, double x, double y, double groesse, Farbe farbe) =>
        ziel.TextSenkrecht(text, X(x), Y(y), groesse * skala, farbe);

    public void Linie(double x1, double y1, double x2, double y2, Farbe farbe, double staerke) =>
        ziel.Linie(X(x1), Y(y1), X(x2), Y(y2), farbe, staerke * skala);

    public void Rechteck(double x, double y, double breite, double hoehe, Farbe farbe) =>
        ziel.Rechteck(X(x), Y(y), breite * skala, hoehe * skala, farbe);

    public void Kreis(double mx, double my, double radius, Farbe fuellung, Farbe rand, double staerke) =>
        ziel.Kreis(X(mx), Y(my), radius * skala, fuellung, rand, staerke * skala);

    private double X(double x) => x0 + (x * skala);

    private double Y(double y) => y0 + (y * skala);
}
