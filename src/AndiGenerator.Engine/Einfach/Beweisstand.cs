// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Einfach;

/// <summary>Was über ein Kriterium des besten Plans bewiesen ist (für die Leiste „bewiesen“ des einfachen Modus).</summary>
public enum Beweisstand
{
    /// <summary>Kein Verstoß – besser geht es nicht.</summary>
    Optimal,

    /// <summary>Bestmöglich zusammen mit allem Vorrangigen: Die Summe seiner Stufe (Pflicht bzw. Wünsche) ist bewiesen erreicht.</summary>
    Bewiesen,

    /// <summary>Das Kriterium allein kann nicht tiefer – aber nicht unbedingt gleichzeitig mit den anderen.</summary>
    AlleinBewiesen,

    /// <summary>Offen: Hier liegt die Abwägung, hierfür gibt es Hebel.</summary>
    Offen,
}
