// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Namen der Runden (Index wie <c>RundeZuDatum</c>) für Terminplan und Diagramme (nicht im Original): sonst Vorrunde und
/// Rückrunde, bei einer Doppelrunde die vier Viertelrunden („1. Vorrunde“ … „2. Rückrunde“), bei einer Doppelrunde als
/// Halbrunde die beiden Runden darin.
/// </summary>
public static class Rundennamen
{
    /// <summary>Die Namen aller Runden der Staffel.</summary>
    /// <param name="optionen">Berechnungsoptionen (Rundenplanung, Doppelrunde).</param>
    /// <returns>Je Runde ein Name.</returns>
    public static IReadOnlyList<string> Fuer(Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        if (!optionen.Doppelrunde)
        {
            return ["Vorrunde", "Rückrunde"];
        }

        return optionen.Rundenplanung == Rundenplanung.Halbrunde
            ? ["1. Runde", "2. Runde"]
            : ["1. Vorrunde", "2. Vorrunde", "1. Rückrunde", "2. Rückrunde"];
    }

    /// <summary>Der Name einer Runde.</summary>
    /// <param name="namen">Die Namen aus <see cref="Fuer"/>.</param>
    /// <param name="runde">Index der Runde; negativ = keiner Runde zugeordnet.</param>
    /// <returns>Der Name; leer ohne Runde.</returns>
    public static string Name(IReadOnlyList<string> namen, int runde)
    {
        ArgumentNullException.ThrowIfNull(namen);
        if (runde < 0)
        {
            return string.Empty;
        }

        return runde < namen.Count ? namen[runde] : string.Create(CultureInfo.InvariantCulture, $"{runde + 1}. Runde");
    }
}
