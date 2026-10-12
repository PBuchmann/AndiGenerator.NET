// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Aufstellungen;

/// <summary>Aufstellung einer Mannschaft aus click-TT: die TTR-Werte ihrer Stammspieler in der Reihenfolge der Meldung.</summary>
/// <param name="Mannschaft">Name der Mannschaft wie in click-TT, z. B. „TTC Wirges II“.</param>
/// <param name="Stammspieler">TTR-Werte der Stammspieler (ohne Reservespieler) nach ihrem Rang in der Mannschaft.</param>
public sealed record Mannschaftsaufstellung(string Mannschaft, IReadOnlyList<int> Stammspieler)
{
    /// <summary>Schnitt der besten Stammspieler nach Rang, z. B. der ersten vier bei Vierer-Mannschaften.</summary>
    /// <param name="anzahl">Zahl der gewerteten Spieler.</param>
    /// <returns>Der Schnitt; 0, wenn es keine Stammspieler gibt.</returns>
    public double Schnitt(int anzahl)
    {
        List<int> gewertet = Stammspieler.Take(Math.Max(1, anzahl)).ToList();
        return gewertet.Count == 0 ? 0.0 : gewertet.Average();
    }
}
