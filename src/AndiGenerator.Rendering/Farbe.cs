// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Farbe einer Zeichnung (RGB, deckend). Die benannten Farben sind die Töne der Oberfläche (<c>Stil.axaml</c>).</summary>
/// <param name="R">Rotanteil.</param>
/// <param name="G">Grünanteil.</param>
/// <param name="B">Blauanteil.</param>
public readonly record struct Farbe(byte R, byte G, byte B)
{
    /// <summary>Holt die Schriftfarbe für Inhalte (fast Schwarz).</summary>
    public static Farbe Tinte { get; } = new(0x1B, 0x1F, 0x24);

    /// <summary>Holt die Schriftfarbe für Nebeninhalte, z. B. Meldungstexte.</summary>
    public static Farbe Tinte2 { get; } = new(0x4A, 0x53, 0x60);

    /// <summary>Holt Grau für Beschriftungen, Spaltenköpfe und Nachbarspiele.</summary>
    public static Farbe Grau { get; } = new(0x5B, 0x64, 0x70);

    /// <summary>Holt die Farbe feiner Trennlinien.</summary>
    public static Farbe Linie { get; } = new(0xE2, 0xE0, 0xDA);

    /// <summary>Holt die Farbe kräftigerer Linien (Tabellenkopf, Summenzeile).</summary>
    public static Farbe Rand { get; } = new(0xC9, 0xC5, 0xBB);

    /// <summary>Holt den hellen Flächenton (Kacheln, Raster).</summary>
    public static Farbe Flaeche { get; } = new(0xF0, 0xEF, 0xEA);

    /// <summary>Holt die Akzentfarbe (Blau, z. B. echte Nachbarmannschaften).</summary>
    public static Farbe Akzent { get; } = new(0x1D, 0x5F, 0xA8);

    /// <summary>Holt das dunkle Akzentblau für Zwischenüberschriften.</summary>
    public static Farbe AkzentDunkel { get; } = new(0x16, 0x4A, 0x84);

    /// <summary>Holt Grün für Erfülltes.</summary>
    public static Farbe Gut { get; } = new(0x1D, 0x6B, 0x3A);

    /// <summary>Holt Rot für Verstöße.</summary>
    public static Farbe Schlecht { get; } = new(0xB4, 0x23, 0x18);

    /// <summary>Holt dunkles Rot für Problemtexte.</summary>
    public static Farbe ProblemDunkel { get; } = new(0x7A, 0x1A, 0x12);

    /// <summary>Holt dunkles Ocker für Hinweise, die zu beachten sind.</summary>
    public static Farbe WarnungDunkel { get; } = new(0x6E, 0x44, 0x00);

    /// <summary>Holt Weiß.</summary>
    public static Farbe Weiss { get; } = new(0xFF, 0xFF, 0xFF);
}
