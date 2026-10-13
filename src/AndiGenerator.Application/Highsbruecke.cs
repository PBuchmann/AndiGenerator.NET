// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Turniere;
using AndiGenerator.Import;

namespace AndiGenerator.Application;

/// <summary>Reicht den Löser HiGHS (Baustein Import) an den Rechenkern weiter (nicht im Original).</summary>
public sealed class Highsbruecke : IGanzzahlloeser
{
    /// <summary>Holt einen Wert, der angibt, ob HiGHS auf diesem Rechner geladen werden kann.</summary>
    public static bool Verfuegbar => HighsLoeser.Verfuegbar;

    /// <inheritdoc/>
    public (double[]? Werte, bool Optimal) Loesen(Ganzzahlmodell modell, double[]? start, int startwert, Func<bool> aufhoeren)
    {
        ArgumentNullException.ThrowIfNull(modell);
        var problem = new Ganzzahlproblem(
            [.. modell.Kosten],
            [.. modell.SpalteOben],
            [.. modell.ZeileUnten],
            [.. modell.ZeileOben],
            [.. modell.ZeilenAnfang],
            [.. modell.EintragSpalten],
            [.. modell.EintragWerte]);
        return HighsLoeser.Loesen(problem, start, startwert, aufhoeren);
    }
}
