// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Wie wichtig ein Feinschliff-Kriterium im normalen Modus sein soll.</summary>
public enum Kriterienwichtigkeit
{
    /// <summary>Nicht so wichtig: darf schlechter werden, wenn dafür anderes besser wird.</summary>
    Niedrig = 0,

    /// <summary>Wie vom Programm vorgesehen.</summary>
    Normal = 1,

    /// <summary>Besonders wichtig: so gut wie möglich optimieren.</summary>
    Hoch = 2,
}
