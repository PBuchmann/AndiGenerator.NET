// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Feld im Kalender der spielfreien Tage.</summary>
/// <param name="Nummer">Tag im Monat; leer bei einem Feld vor dem Ersten.</param>
/// <param name="Zeile">Der wählbare Tag (er hat Heimspielwünsche); <c>null</c>, wenn der Tag nicht wählbar ist.</param>
/// <param name="Wochenende">Samstag oder Sonntag.</param>
/// <param name="Hinweis">Tooltip: Datum und, bei nicht wählbaren Tagen, der Grund, z. B. „keine Mannschaft hat hier einen Heimspielwunsch“.</param>
/// <param name="Eingetragen">Nicht wählbar, aber als spielfreier Tag eingetragen (z. B. aus click-TT).</param>
public sealed record Kalendertag(string Nummer, AuswahlZeile? Zeile, bool Wochenende, string Hinweis, bool Eingetragen = false)
{
    /// <summary>Holt einen Wert, der angibt, ob der Tag gewählt werden kann.</summary>
    public bool Waehlbar => Zeile is not null;

    /// <summary>Holt einen Wert, der angibt, ob es ein Tag des Monats ist (kein leeres Feld).</summary>
    public bool IstTag => Nummer.Length > 0;
}
