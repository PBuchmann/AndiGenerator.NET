// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Neu-Würfel-Strategie eines Workers (Original <c>TPlanCalcThread.SetThreadType</c>).</summary>
/// <param name="Typ">Art des Neu-Würfelns.</param>
/// <param name="Anzahl">Anzahl der Spieltage bzw. Mannschaften.</param>
/// <param name="Prozent">Anteil der neu zu würfelnden Spiele.</param>
internal sealed record Tauschstrategie(Tauschtyp Typ, int Anzahl, double Prozent)
{
    /// <summary>Liest eine Strategie wie <c>R</c>, <c>15</c>, <c>S1,25</c> oder <c>M2,10</c> (Original <c>cThreadTauschPercent</c>).</summary>
    public static Tauschstrategie Lesen(string text) => text[0] switch
    {
        'R' => new Tauschstrategie(Tauschtyp.Raster, 0, 10),
        'S' => new Tauschstrategie(Tauschtyp.Spieltag, text[1] - '0', double.Parse(text[3..], CultureInfo.InvariantCulture)),
        'M' => new Tauschstrategie(Tauschtyp.Mannschaft, text[1] - '0', double.Parse(text[3..], CultureInfo.InvariantCulture)),
        _ => new Tauschstrategie(Tauschtyp.Normal, 0, double.Parse(text, CultureInfo.InvariantCulture)),
    };
}
