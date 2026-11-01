// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zeile „km je Mannschaft“.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Zusatz">z. B. „1× Ausrichter“.</param>
/// <param name="Km">Kilometer als Text.</param>
/// <param name="Anteil">Länge des Balkens von 0 bis 1 (1 = weiteste Mannschaft).</param>
/// <param name="Gewaehlt">Mannschaft ist ausgewählt.</param>
public sealed record Kompaktkmzeile(string Name, string Zusatz, string Km, double Anteil, bool Gewaehlt)
{
    /// <summary>Holt die Breite des Balkens bei 90 Bildpunkten für den längsten.</summary>
    public double Balken => Math.Max(2, Anteil * 90);
}
