// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Form eines Turniertags.</summary>
public enum Turnierform
{
    /// <summary>Spiele, die zu keiner bekannten Form passen.</summary>
    Unbekannt,

    /// <summary>3 Mannschaften, jeder gegen jeden (3 Spiele an zwei Tischen nacheinander, z. B. 10, 13 und 16 Uhr).</summary>
    Dreier,

    /// <summary>
    /// Einzelbegegnung: Bei gerader Mannschaftszahl bleibt für jede Mannschaft eine Paarung übrig, die einzeln
    /// (möglichst mit kurzer Fahrt) ausgetragen wird.
    /// </summary>
    Einzel,

    /// <summary>
    /// 4 Mannschaften in zwei Runden an zwei Tischen (4 Spiele, z. B. 11 und 14 Uhr): Ausrichter und eine zweite
    /// Mannschaft spielen jeweils gegen die beiden übrigen, aber nicht gegeneinander.
    /// </summary>
    Vierer,
}
