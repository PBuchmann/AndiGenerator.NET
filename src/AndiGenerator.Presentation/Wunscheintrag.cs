// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile einer Terminwunsch-Karte.</summary>
/// <param name="Text">Der Text wie im Original.</param>
/// <param name="Farbe">Die Hervorhebung des Originals (orange: Halle belegt, rot: Problem, grün: genügend Termine).</param>
public sealed record Wunscheintrag(string Text, Terminwunschfarbe Farbe)
{
    /// <summary>Holt einen Wert, der angibt, ob die Zeile hervorgehoben ist.</summary>
    public bool Hervorgehoben => Farbe != Terminwunschfarbe.Normal;
}
