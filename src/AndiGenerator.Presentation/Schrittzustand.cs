// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Stand eines Schritts der Einrichtung.</summary>
public enum Schrittzustand
{
    /// <summary>Noch nicht bearbeitet.</summary>
    Offen,

    /// <summary>Übernommen.</summary>
    Erledigt,

    /// <summary>Übersprungen (nichts geändert).</summary>
    Uebersprungen,
}
