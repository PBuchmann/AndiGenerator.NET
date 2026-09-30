// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Application;

/// <summary>Gewichtung einer Kostenart für alle Mannschaften (Original <c>TActionRectKostenTypeMannschaft</c>, Spaltenkopf).</summary>
/// <param name="Art">Die Kostenart.</param>
public sealed record KostenartGewichtungsziel(MannschaftsKostenart Art) : Gewichtungsziel
{
    /// <inheritdoc/>
    public override string Beschriftung => Gewichtungsanzeige.Name(Art);

    /// <inheritdoc/>
    public override Gewichtung Lesen(Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        return optionen.Fuer(Art);
    }

    /// <inheritdoc/>
    public override Berechnungsoptionen Setzen(Berechnungsoptionen optionen, Gewichtung wert)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        Gewichtung[] neu = optionen.JeKostenart.ToArray();
        neu[(int)Art] = wert;
        return optionen with { JeKostenart = neu };
    }
}
