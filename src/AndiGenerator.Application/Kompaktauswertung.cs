// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Auswertung eines Plans mit Kompaktspieltagen gegen die Meldungen (nicht im Original).</summary>
/// <param name="Tage">Die Sonntage der Halbrunde.</param>
/// <param name="Mannschaften">Die Mannschaften in der Reihenfolge der Staffel.</param>
/// <param name="Spieltage">Die erkannten Kompaktspieltage nach Datum.</param>
/// <param name="Befunde">Alle Befunde als Text (Verstöße, fehlende Meldungen, Widersprüche).</param>
/// <param name="GroessterAbstand">Größter Abstand im Stand: Unterschied der gespielten Spiele zwischen zwei Mannschaften.</param>
/// <param name="MeisteAmTag">Größte Zahl von Mannschaften, die an einem Sonntag spielen.</param>
public sealed record Kompaktauswertung(
    IReadOnlyList<DateOnly> Tage,
    IReadOnlyList<Kompaktmannschaft> Mannschaften,
    IReadOnlyList<Kompaktspieltag> Spieltage,
    IReadOnlyList<string> Befunde,
    int GroessterAbstand,
    int MeisteAmTag)
{
    /// <summary>Holt die Kilometer Luftlinie aller Mannschaften, hin und zurück.</summary>
    public double KmGesamt => Mannschaften.Sum(m => m.Km);

    /// <summary>Holt die Zahl der Verstöße gegen die Meldungen.</summary>
    public int Verstoesse => Spieltage.Sum(s => s.Hinweise.Count);

    /// <summary>Holt die Zahl der Mannschaften ohne Heimspieltag.</summary>
    public int OhneHeimspieltag => Mannschaften.Count(m => m.Ausgerichtet == 0);
}
