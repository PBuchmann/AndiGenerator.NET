// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Hervorhebung einer Zeile im Tab „Terminwünsche“ des Originals.</summary>
public enum Terminwunschfarbe
{
    /// <summary>Normal.</summary>
    Normal,

    /// <summary>Halle belegt (orange).</summary>
    Gelb,

    /// <summary>Spielfreier Tag, zu wenige Termine oder nicht erfüllbare Wünsche (rot).</summary>
    Rot,

    /// <summary>Genügend Termine (grün).</summary>
    Gruen,
}
