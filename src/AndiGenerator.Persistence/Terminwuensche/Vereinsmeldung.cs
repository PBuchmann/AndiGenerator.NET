// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Aufstellungen;

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>Meldung einer Mannschaft im click-TT-Bericht „Terminwünsche“ (ohne Kontaktdaten).</summary>
/// <param name="Verein">Name des Vereins, z. B. „TTC Wirges“.</param>
/// <param name="VereinsNummer">Vereinsnummer aus click-TT (in der XML-Datei <c>clubId</c>).</param>
/// <param name="Klasse">Altersklasse mit Mannschaftsnummer, z. B. „Erwachsene II“ oder „Jugend 19“.</param>
/// <param name="Spiellokale">Spiellokale 1 bis 3 des Vereins (nur angegebene).</param>
/// <param name="Bemerkung">Bemerkung des Vereins (Freitext), leer wenn keine.</param>
/// <param name="Heimtermine">Terminwünsche der Vor- und Rückrunde.</param>
/// <param name="Sperrtermine">Sperrtermine der Vor- und Rückrunde.</param>
public sealed record Vereinsmeldung(
    string Verein,
    int VereinsNummer,
    string Klasse,
    IReadOnlyDictionary<int, Spiellokaladresse> Spiellokale,
    string Bemerkung,
    IReadOnlyList<GemeldeterHeimtermin> Heimtermine,
    IReadOnlyList<DateOnly> Sperrtermine)
{
    /// <summary>Holt die Mannschaftsnummer aus der Klasse („Erwachsene II“ → 2, „Jugend 19“ → 1).</summary>
    public int MannschaftsNummer => MannschaftsaufstellungenCsv.Nummer(Klasse);
}
