// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Stammdaten;

/// <summary>Verbindlichkeit eines Koppeltermins oder Doppelspieltags (Original <c>coupleprio</c>).</summary>
public enum Koppelprioritaet
{
    /// <summary>Koppelung möglich (jeder Wert außer 1 und 2).</summary>
    Moeglich = 0,

    /// <summary>Koppelung erwünscht (<c>coupleprio</c> = 1, click-TT <c>soft</c>).</summary>
    Weich = 1,

    /// <summary>Koppelung verbindlich (<c>coupleprio</c> = 2, click-TT <c>hard</c>).</summary>
    Hart = 2,
}
