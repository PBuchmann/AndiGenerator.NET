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

    /// <summary>Zeichnet einen Kreis, gefüllt und/oder mit Rand.</summary>
    /// <param name="mx">Mittelpunkt x.</param>
    /// <param name="my">Mittelpunkt y.</param>
    /// <param name="radius">Radius.</param>
    /// <param name="fuellung">Füllfarbe oder <c>null</c>.</param>
    /// <param name="rand">Randfarbe oder <c>null</c>.</param>
    /// <param name="staerke">Randstärke.</param>
    void Kreis(double mx, double my, double radius, Farbe? fuellung, Farbe? rand, double staerke);

    /// <summary>Zeichnet ein Vieleck bzw. einen Linienzug, gefüllt und/oder mit Rand (spitze Ecken, gerade Enden).</summary>
    /// <param name="punkte">Die Eckpunkte.</param>
    /// <param name="fuellung">Füllfarbe oder <c>null</c>.</param>
    /// <param name="rand">Randfarbe oder <c>null</c>.</param>
    /// <param name="staerke">Randstärke.</param>
    /// <param name="geschlossen">Ob der Linienzug zum ersten Punkt zurückkehrt.</param>
    void Vieleck(IReadOnlyList<(double X, double Y)> punkte, Farbe? fuellung, Farbe? rand, double staerke, bool geschlossen);

    /// <summary>Schreibt einen Text mit einem Rand um die Buchstaben (z. B. einem hellen Hof auf einer Karte).</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="x">Linke Kante.</param>
    /// <param name="y">Oberkante.</param>
    /// <param name="groesse">Schriftgröße in Punkt.</param>
    /// <param name="farbe">Schriftfarbe.</param>
    /// <param name="fett">Fettschrift.</param>
    /// <param name="rand">Farbe des Rands.</param>
    /// <param name="staerke">Breite des Rands.</param>
    void TextMitRand(string text, double x, double y, double groesse, Farbe farbe, bool fett, Farbe rand, double staerke);

    /// <summary>Zeichnet alles bis zum Ende des Bereichs mit verringerter Deckkraft.</summary>
    /// <param name="wert">Deckkraft von 0 bis 1.</param>
    /// <returns>Der Bereich; <see cref="IDisposable.Dispose"/> beendet ihn.</returns>
    IDisposable Deckkraft(double wert);

    /// <summary>Beschneidet alles bis zum Ende des Bereichs auf ein Rechteck.</summary>
    /// <param name="x">Linke Kante.</param>
    /// <param name="y">Oberkante.</param>
    /// <param name="breite">Breite.</param>
    /// <param name="hoehe">Höhe.</param>
    /// <returns>Der Bereich; <see cref="IDisposable.Dispose"/> beendet ihn.</returns>
    IDisposable Begrenzen(double x, double y, double breite, double hoehe);
}
