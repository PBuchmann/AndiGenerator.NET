// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Domain.Turniere;

/// <summary>Was eine Mannschaft an einem Turniertag (Sonntag) kann – Antwort in der Meldung der Vereine.</summary>
[Flags]
public enum Turnierbereitschaft
{
    /// <summary>An diesem Tag kann die Mannschaft nicht spielen.</summary>
    None = 0,

    /// <summary>Möglicher Heimspieltag: Die Mannschaft kann ein Turnier in ihrer Halle ausrichten.</summary>
    Heim = 1,

    /// <summary>Möglicher Auswärtsspieltag: Die Mannschaft kann bei einem Turnier in einer fremden Halle spielen.</summary>
    Auswaerts = 2,
}
