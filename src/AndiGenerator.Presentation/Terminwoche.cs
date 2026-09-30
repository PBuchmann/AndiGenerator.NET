// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Kalenderwoche des Spielplans.</summary>
/// <param name="Titel">Z. B. „KW 38“.</param>
/// <param name="Zeitraum">Z. B. „14.09.–20.09.2026“.</param>
/// <param name="Rundenwechsel">Name der Runde, wenn mit dieser Woche eine neue beginnt, sonst leer.</param>
/// <param name="Spiele">Die Spiele der Woche.</param>
public sealed record Terminwoche(string Titel, string Zeitraum, string Rundenwechsel, IReadOnlyList<Terminspiel> Spiele)
{
    /// <summary>Holt einen Wert, der angibt, ob vor der Woche eine neue Runde beginnt.</summary>
    public bool HatRundenwechsel => Rundenwechsel.Length > 0;

    /// <summary>Holt die Zahl der Spiele als Text.</summary>
    public string Anzahl => Spiele.Count == 1 ? "1 Spiel" : Spiele.Count + " Spiele";
}
