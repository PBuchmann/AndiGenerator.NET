// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Application;

/// <summary>Gewichtung einer Plan-Kostenart (Original <c>TActionRectKostenPlan</c>).</summary>
/// <param name="Art">Die Plan-Kostenart.</param>
public sealed record PlanGewichtungsziel(PlanKostenart Art) : Gewichtungsziel
{
    /// <inheritdoc/>
    public override string Beschriftung => Gewichtungsanzeige.Name(Art);

    /// <inheritdoc/>
    public override Gewichtung Lesen(Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        return Art switch
        {
            PlanKostenart.UeberlappungSpieltage => optionen.SpieltagUeberlappung,
            PlanKostenart.LaengeSpieltage => optionen.Spieltag,
            PlanKostenart.UeberlappungLetzterSpieltag => optionen.LetzterSpieltagUeberlappung,
            PlanKostenart.LaengeLetzterSpieltag => optionen.LetzterSpieltag,
            _ => optionen.VereinsinterneSpieleAmAnfang,
        };
    }

    /// <inheritdoc/>
    public override Berechnungsoptionen Setzen(Berechnungsoptionen optionen, Gewichtung wert)
    {
        ArgumentNullException.ThrowIfNull(optionen);
        return Art switch
        {
            PlanKostenart.UeberlappungSpieltage => optionen with { SpieltagUeberlappung = wert },
            PlanKostenart.LaengeSpieltage => optionen with { Spieltag = wert },
            PlanKostenart.UeberlappungLetzterSpieltag => optionen with { LetzterSpieltagUeberlappung = wert },
            PlanKostenart.LaengeLetzterSpieltag => optionen with { LetzterSpieltag = wert },
            _ => optionen with { VereinsinterneSpieleAmAnfang = wert },
        };
    }
}
