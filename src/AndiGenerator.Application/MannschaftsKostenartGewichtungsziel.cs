// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Application;

/// <summary>Gewichtung einer Kostenart bei einer Mannschaft (Original <c>TActionRectKostenMannschaftDetail</c>, Tabellenzelle).</summary>
/// <param name="MannschaftsId">click-TT-Id der Mannschaft.</param>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Art">Die Kostenart.</param>
public sealed record MannschaftsKostenartGewichtungsziel(string MannschaftsId, string Name, MannschaftsKostenart Art) : Gewichtungsziel
{
    /// <inheritdoc/>
    public override string Beschriftung => Gewichtungsanzeige.Name(Art);

    /// <inheritdoc/>
    public override Gewichtung Lesen(Berechnungsoptionen optionen) =>
        Eintrag(optionen, MannschaftsId)?.JeKostenart[(int)Art] ?? Gewichtung.Normal;

    /// <inheritdoc/>
    public override Berechnungsoptionen Setzen(Berechnungsoptionen optionen, Gewichtung wert) =>
        EintragSetzen(optionen, MannschaftsId, e =>
        {
            Gewichtung[] neu = e.JeKostenart.ToArray();
            neu[(int)Art] = wert;
            return e with { JeKostenart = neu };
        });
}
