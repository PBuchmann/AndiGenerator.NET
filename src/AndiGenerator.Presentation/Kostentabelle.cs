// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Kostentabelle je Mannschaft (Original: Tabelle im Tab „Abweichungen“).</summary>
/// <param name="Spalten">Köpfe der sichtbaren Kostenarten (Klick: Gewichtung der Kostenart für alle Mannschaften).</param>
/// <param name="Zeilen">Zeilen je Mannschaft in der Reihenfolge des Originals.</param>
/// <param name="Summen">Summen je Kostenart mit der Gewichtungsmarke der Kostenart.</param>
/// <param name="Gesamtsumme">Summe aller Mannschaftskosten.</param>
public sealed record Kostentabelle(IReadOnlyList<Kostenfeld> Spalten, IReadOnlyList<Kostenzeile> Zeilen, IReadOnlyList<Kostenfeld> Summen, Kostenfeld Gesamtsumme)
{
    /// <summary>Holt die leere Tabelle.</summary>
    public static Kostentabelle Leer { get; } = new([], [], [], Kostenfeld.Nur(string.Empty));
}
