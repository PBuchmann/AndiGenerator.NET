// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Namen der Gewichtungsstufen für Dialoge der Oberfläche.</summary>
public static class Gewichtungsnamen
{
    /// <summary>Holt alle Stufen in der Reihenfolge des Originals.</summary>
    public static IReadOnlyList<Gewichtung> Stufen => Gewichtungsanzeige.AlleStufen;

    /// <summary>Name einer Stufe.</summary>
    /// <param name="wert">Die Stufe.</param>
    /// <returns>Z. B. <c>sehr hoch</c>.</returns>
    public static string Name(Gewichtung wert) => Gewichtungsanzeige.Name(wert);
}
