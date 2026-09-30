// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Was in der Kostenansicht ausgewählt ist und rechts in den Details erscheint.</summary>
public enum Kostenauswahlart
{
    /// <summary>Eine Zelle: eine Kostenart bei einer Mannschaft.</summary>
    Zelle,

    /// <summary>Eine Kostenart für alle Mannschaften (Spaltenkopf).</summary>
    Kostenart,

    /// <summary>Eine Mannschaft mit allen Kostenarten (Zeilenkopf).</summary>
    Mannschaft,

    /// <summary>Eine Kennzahl des ganzen Plans (Kachel über der Tabelle).</summary>
    Kennzahl,
}
