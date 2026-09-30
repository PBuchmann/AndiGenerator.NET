// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Abschnitt einer Terminwunsch-Karte unter einer Zwischenüberschrift des Originals.</summary>
/// <param name="Titel">Die Zwischenüberschrift oder leer.</param>
/// <param name="Eintraege">Die Zeilen.</param>
public sealed record Wunschabschnitt(string Titel, IReadOnlyList<Wunscheintrag> Eintraege)
{
    /// <summary>Holt einen Wert, der angibt, ob der Abschnitt eine Überschrift hat.</summary>
    public bool HatTitel => Titel.Length > 0;
}
