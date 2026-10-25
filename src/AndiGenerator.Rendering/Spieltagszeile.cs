// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Eine Zeile der Zeitleiste: ein Spieltag mit seinen Terminen.</summary>
/// <param name="Nummer">Nummer des Spieltags (ab 1).</param>
/// <param name="Termine">Die Wochen mit Spielen, geordnet.</param>
/// <param name="Ueberschneidungen">Die Wochen (Montag bis Montag), in denen andere Spieltage laufen.</param>
/// <param name="Mit">Die Nummern der Spieltage, mit denen er sich überschneidet.</param>
/// <param name="WieVorher">Alle Mannschaften spielen ihn am selben Tag wie den vorigen (Kompakt- oder Doppelspieltag).</param>
public sealed record Spieltagszeile(
    int Nummer,
    IReadOnlyList<Spieltagstermin> Termine,
    IReadOnlyList<Spieltagsabschnitt> Ueberschneidungen,
    IReadOnlyList<int> Mit,
    bool WieVorher)
{
    /// <summary>Holt den Montag der ersten Woche des Spieltags.</summary>
    public DateOnly Von => Termine[0].Woche;

    /// <summary>Holt den Montag der letzten Woche des Spieltags.</summary>
    public DateOnly Bis => Termine[^1].Woche;

    /// <summary>Holt die Wochen von der ersten bis zur letzten Woche des Spieltags (0: alle in einer Woche).</summary>
    public int Wochen => (Bis.DayNumber - Von.DayNumber) / 7;
}
