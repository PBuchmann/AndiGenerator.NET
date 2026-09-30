// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Zeichenfläche wie <c>TZoomCanvas</c> im Original: dieselben Zeichenroutinen schreiben auf den Bildschirm oder in
/// eine PDF-Datei. Einheiten sind Punkte (1/72 Zoll), Koordinaten von links oben; Texte werden an ihrer Oberkante
/// ausgerichtet.
/// </summary>
public interface IZeichenflaeche
{
    /// <summary>Misst die Breite eines Textes.</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="groesse">Schriftgröße in Punkt.</param>
    /// <param name="fett">Fettschrift.</param>
    /// <returns>Die Breite.</returns>
    double TextBreite(string text, double groesse, bool fett);

    /// <summary>Schreibt einen Text.</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="x">Linke Kante.</param>
    /// <param name="y">Oberkante.</param>
    /// <param name="groesse">Schriftgröße in Punkt.</param>
    /// <param name="farbe">Schriftfarbe.</param>
    /// <param name="fett">Fettschrift.</param>
    void Text(string text, double x, double y, double groesse, Farbe farbe, bool fett);

    /// <summary>
    /// Schreibt einen Text senkrecht von unten nach oben (Original <c>FontRotation = -90</c>): der um 90° gegen den
    /// Uhrzeigersinn gedrehte Text beginnt bei (<paramref name="x"/>, <paramref name="y"/>) und läuft nach oben; seine
    /// Oberkante liegt links bei <paramref name="x"/>.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="x">Linke Kante (Oberkante der Schrift).</param>
    /// <param name="y">Unteres Ende (Textanfang).</param>
    /// <param name="groesse">Schriftgröße in Punkt.</param>
    /// <param name="farbe">Schriftfarbe.</param>
    void TextSenkrecht(string text, double x, double y, double groesse, Farbe farbe);

    /// <summary>Zeichnet eine Linie.</summary>
    /// <param name="x1">Anfang x.</param>
    /// <param name="y1">Anfang y.</param>
    /// <param name="x2">Ende x.</param>
    /// <param name="y2">Ende y.</param>
    /// <param name="farbe">Farbe.</param>
    /// <param name="staerke">Linienstärke.</param>
    void Linie(double x1, double y1, double x2, double y2, Farbe farbe, double staerke);

    /// <summary>Füllt ein Rechteck.</summary>
    /// <param name="x">Linke Kante.</param>
    /// <param name="y">Oberkante.</param>
    /// <param name="breite">Breite.</param>
    /// <param name="hoehe">Höhe.</param>
    /// <param name="farbe">Füllfarbe.</param>
    void Rechteck(double x, double y, double breite, double hoehe, Farbe farbe);

    /// <summary>Zeichnet einen gefüllten Kreis mit Rand.</summary>
    /// <param name="mx">Mittelpunkt x.</param>
    /// <param name="my">Mittelpunkt y.</param>
    /// <param name="radius">Radius.</param>
    /// <param name="fuellung">Füllfarbe.</param>
    /// <param name="rand">Randfarbe.</param>
    /// <param name="staerke">Randstärke.</param>
    void Kreis(double mx, double my, double radius, Farbe fuellung, Farbe rand, double staerke);
}
