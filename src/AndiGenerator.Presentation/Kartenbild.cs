// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Fertig berechnete Karte der Spielorte (nicht im Original): Umrisse der Bundesländer, einige Städte, Hallen und Fahrten,
/// in Bildpunkten eines Bilds von <see cref="Breite"/> × <see cref="Hoehe"/>. Die Oberfläche skaliert nur noch.
/// </summary>
/// <param name="Breite">Breite des Bilds.</param>
/// <param name="Hoehe">Höhe des Bilds.</param>
/// <param name="Flaechen">Umrisse der Bundesländer als geschlossene Linienzüge.</param>
/// <param name="Laender">Namen der sichtbaren Bundesländer.</param>
/// <param name="Staedte">Einige Städte zur Orientierung.</param>
/// <param name="Hallen">Die Spielorte mit Beschriftung.</param>
/// <param name="Fahrten">Die Fahrten.</param>
/// <param name="MassstabKm">Länge des Maßstabs in Kilometern.</param>
/// <param name="MassstabBreite">Länge des Maßstabs in Bildpunkten.</param>
public sealed record Kartenbild(
    double Breite,
    double Hoehe,
    IReadOnlyList<IReadOnlyList<Kartenpunkt>> Flaechen,
    IReadOnlyList<Kartenbeschriftung> Laender,
    IReadOnlyList<Kartenbeschriftung> Staedte,
    IReadOnlyList<Kartenhalle> Hallen,
    IReadOnlyList<Kartenfahrt> Fahrten,
    int MassstabKm,
    double MassstabBreite)
{
    /// <summary>Holt ein leeres Bild (keine Spielorte bekannt).</summary>
    public static Kartenbild Leer { get; } = new(600, 300, [], [], [], [], [], 0, 0);
}
