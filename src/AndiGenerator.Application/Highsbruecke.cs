// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Turniere;
using AndiGenerator.Import;

namespace AndiGenerator.Application;

/// <summary>Reicht den Löser HiGHS (Baustein Import) an den Rechenkern weiter (nicht im Original).</summary>
public sealed class Highsbruecke : IGanzzahlloeser, ISchrankenloeser
{
    /// <summary>Holt einen Wert, der angibt, ob HiGHS auf diesem Rechner geladen werden kann.</summary>
    public static bool Verfuegbar => HighsLoeser.Verfuegbar;

    /// <inheritdoc/>
    public (double[]? Werte, bool Optimal) Loesen(Ganzzahlmodell modell, double[]? start, int startwert, Func<bool> aufhoeren, TimeSpan? zeitgrenze = null)
    {
        ArgumentNullException.ThrowIfNull(modell);
        return HighsLoeser.Loesen(Problem(modell), start, startwert, aufhoeren, zeitgrenze);
    }

    /// <inheritdoc/>
    public Schrankenloesung Loesen(Ganzzahlmodell modell, TimeSpan zeitgrenze, double[]? start = null, Func<bool>? aufhoeren = null, Laufzeitregel? regel = null)
    {
        ArgumentNullException.ThrowIfNull(modell);
        (double[]? werte, double wert, double schranke, bool optimal, bool unloesbar) = regel is null
            ? HighsLoeser.LoesenMitSchranke(Problem(modell), zeitgrenze, start, aufhoeren)
            : HighsLoeser.LoesenMitSchranke(Problem(modell), regel.Hoechstens, start, aufhoeren, new Fortschrittsgrenze(regel.Mindestens.TotalSeconds, regel.Stillstand.TotalSeconds));
        return new Schrankenloesung(werte, wert, schranke, optimal, unloesbar);
    }

    private static Ganzzahlproblem Problem(Ganzzahlmodell modell) => new(
        [.. modell.Kosten],
        [.. modell.SpalteOben],
        [.. modell.ZeileUnten],
        [.. modell.ZeileOben],
        [.. modell.ZeilenAnfang],
        [.. modell.EintragSpalten],
        [.. modell.EintragWerte]);
}
