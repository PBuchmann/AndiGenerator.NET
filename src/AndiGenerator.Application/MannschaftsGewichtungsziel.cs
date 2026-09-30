// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Application;

/// <summary>Gewichtung aller Kosten einer Mannschaft (Original <c>TActionRectKostenMannschaftMain</c>, Mannschaftsname).</summary>
/// <param name="MannschaftsId">click-TT-Id der Mannschaft.</param>
/// <param name="Name">Name der Mannschaft (Beschriftung).</param>
public sealed record MannschaftsGewichtungsziel(string MannschaftsId, string Name) : Gewichtungsziel
{
    /// <inheritdoc/>
    public override string Beschriftung => Name;

    /// <inheritdoc/>
    public override Gewichtung Lesen(Berechnungsoptionen optionen) =>
        Eintrag(optionen, MannschaftsId)?.Gesamt ?? Gewichtung.Normal;

    /// <inheritdoc/>
    public override Berechnungsoptionen Setzen(Berechnungsoptionen optionen, Gewichtung wert) =>
        EintragSetzen(optionen, MannschaftsId, e => e with { Gesamt = wert });
}
