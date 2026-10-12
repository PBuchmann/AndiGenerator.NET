// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>Abgleich einer Mannschaft der Staffel mit dem Bericht „Terminwünsche“.</summary>
/// <param name="Mannschaft">Name der Mannschaft.</param>
/// <param name="Gefunden">Die Mannschaft steht im Bericht.</param>
/// <param name="Spiellokale">Spiellokale des Vereins als Text, z. B. „1: Haybachhalle, Am Bandweidenweg, 55270 Klein-Winternheim“.</param>
/// <param name="NummernGesetzt">Zahl der Heimtermine, deren Spiellokal-Nummer aus dem Hallennamen gesetzt wird.</param>
/// <param name="Bemerkung">Bemerkung des Vereins, leer wenn keine.</param>
/// <param name="Abweichungen">Unterschiede zwischen Bericht und Spielplandaten (Heim- und Sperrtermine).</param>
public sealed record Mannschaftsabgleich(
    string Mannschaft,
    bool Gefunden,
    IReadOnlyList<string> Spiellokale,
    int NummernGesetzt,
    string Bemerkung,
    IReadOnlyList<string> Abweichungen);
