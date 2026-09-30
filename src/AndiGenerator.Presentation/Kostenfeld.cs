// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Ein Feld der Kostenansicht wie im Original: Wert, Gewichtungsmarke und Hinterlegung nach Kostenanteil.</summary>
/// <param name="Text">Angezeigter Wert, z. B. <c>2 /8 (10.000)</c>.</param>
/// <param name="Markierung">Gewichtungsmarke, z. B. <c>+1</c>, <c>-2</c> oder <c>X</c>; leer bei „normal“.</param>
/// <param name="Anteil">Anteil an den Gesamtkosten (0 … 1) für die rote Hinterlegung.</param>
/// <param name="Ziel">Gewichtung, die ein Klick ändert; <c>null</c>, wenn das Feld nicht anklickbar ist.</param>
public sealed record Kostenfeld(string Text, string Markierung, double Anteil, Gewichtungsziel? Ziel)
{
    /// <summary>Erzeugt ein reines Textfeld (Beschriftung, nicht anklickbar).</summary>
    /// <param name="text">Der Text.</param>
    /// <returns>Das Feld.</returns>
    public static Kostenfeld Nur(string text) => new(text, string.Empty, 0, null);
}
