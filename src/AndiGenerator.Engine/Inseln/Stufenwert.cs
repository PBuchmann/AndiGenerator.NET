// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Bewertung eines Plans im Automodus (MIGRATIONSPLAN Abschnitt 11), immer mit den Gewichtungen des Anwenders:
/// Zahl der harten Fehler, der Verstöße der Stufen A und B (E14), der C-Ausreißer (Mannschaften, die bei einem C-Kriterium
/// deutlich schlechter dastehen als im Grundlauf) und der C-Verstöße, zuletzt die C-Kosten. Verglichen wird in dieser
/// Reihenfolge; die Kosten entscheiden erst bei gleichen Anzahlen.
/// </summary>
/// <param name="Harte">Nicht terminierte, nicht erlaubte Spiele und Spiele an spielfreien Tagen.</param>
/// <param name="A">Verstöße der Stufe A (Anzahlen über alle Mannschaften, ein Plan-Kriterium zählt 1).</param>
/// <param name="B">Verstöße der Stufe B.</param>
/// <param name="Ausreisser">C-Ausreißer: Mannschaften × C-Kriterien über dem erlaubten Spielraum (ohne Grundlauf: 0).</param>
/// <param name="CAnzahl">Verstöße der Kriterien der Stufe C (Spieltagslänge und -überlappung haben keine Anzahl).</param>
/// <param name="C">Kosten der Kriterien der Stufe C.</param>
public readonly record struct Stufenwert(int Harte, int A, int B, int Ausreisser, int CAnzahl, double C)
{
    /// <summary>Weniger harte Fehler, dann weniger A-, B-Verstöße, C-Ausreißer und C-Verstöße, bei gleichen Anzahlen kleinere C-Kosten.</summary>
    /// <param name="andere">Der Vergleichswert.</param>
    /// <returns><c>true</c>, wenn dieser Wert besser ist.</returns>
    public bool IstBesserAls(Stufenwert andere)
    {
        int vergleich = (Harte, A, B, Ausreisser, CAnzahl).CompareTo((andere.Harte, andere.A, andere.B, andere.Ausreisser, andere.CAnzahl));
        return vergleich != 0 ? vergleich < 0 : C < andere.C;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        (Harte > 0 ? $"H {Harte}, " : string.Empty) + $"A {A}, B {B}, C {CAnzahl} ({C:N0})" + (Ausreisser > 0 ? $", C-Ausreißer {Ausreisser}" : string.Empty);
}
