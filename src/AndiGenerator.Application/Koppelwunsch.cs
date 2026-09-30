// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Koppelwunsch an einem Heimspieltag (Teil von Original <c>TWunschterminOption</c>).</summary>
public enum Koppelwunsch
{
    /// <summary>Zwei Heimspiele an diesem Tag möglich.</summary>
    Moeglich,

    /// <summary>Zwei Heimspiele an diesem Tag gewünscht.</summary>
    Gewuenscht,

    /// <summary>Zwei Heimspiele an diesem Tag gewünscht (hohe Prio).</summary>
    Hoch,

    /// <summary>Doppelspieltag möglich.</summary>
    DoppelMoeglich,

    /// <summary>Doppelspieltag gewünscht.</summary>
    DoppelGewuenscht,

    /// <summary>Doppelspieltag gewünscht (hohe Prio).</summary>
    DoppelHoch,

    /// <summary>Kein Koppeltermin/Doppelspieltag.</summary>
    Keiner,
}
