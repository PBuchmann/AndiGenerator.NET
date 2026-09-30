// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Art einer Zeile in der Terminplanansicht.</summary>
public enum Planzeilenart
{
    /// <summary>Spiel dieser Staffel.</summary>
    Spiel,

    /// <summary>Überschrift, z. B. der Name der Mannschaft.</summary>
    Ueberschrift,

    /// <summary>Spiel einer Vereinsmannschaft am selben Tag (grau).</summary>
    Nachbarspiel,

    /// <summary>Spiel einer echten Nachbarmannschaft am selben Tag (blau).</summary>
    EchtesNachbarspiel,

    /// <summary>Leerzeile (neue Woche, neue Runde).</summary>
    Abstand,
}
