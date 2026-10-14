// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein geplanter Kompaktspieltag (nicht im Original): Tag, Form und Mannschaften in fester Reihenfolge.</summary>
/// <param name="Tag">Der Tag.</param>
/// <param name="Form">Dreier, Vierer oder Einzelbegegnung.</param>
/// <param name="Mannschaften">
/// Der Ausrichter zuerst. Beim Vierer an zweiter Stelle die Mannschaft, die mit dem Ausrichter gegen die beiden übrigen
/// spielt (in click-TT bei einem ihrer beiden Spiele formal Heimmannschaft, verlegt in die Halle des Ausrichters); bei der
/// Einzelbegegnung Heim- und Gastmannschaft.
/// </param>
public sealed record Planturnier(DateOnly Tag, Turnierform Form, IReadOnlyList<string> Mannschaften)
{
    /// <summary>Holt den Ausrichter (bei der Einzelbegegnung die Heimmannschaft).</summary>
    public string Ausrichter => Mannschaften[0];

    /// <summary>
    /// Die Begegnungen nach Runden: beim Vierer zwei Runden mit je zwei Spielen parallel (Ausrichter – Gast 1,
    /// Partner – Gast 2; Ausrichter – Gast 2, Gast 1 – Partner – so ist der Partner nur einmal „Heimmannschaft“,
    /// Peter, 13.10.2026), beim Dreier drei Runden
    /// (Ausrichter – Gast 1, Gast 1 – Gast 2, Ausrichter – Gast 2), bei der Einzelbegegnung ein Spiel.
    /// </summary>
    /// <returns>Runde (ab 0), Heim- und Gastmannschaft.</returns>
    public IReadOnlyList<(int Runde, string Heim, string Gast)> Begegnungen()
    {
        IReadOnlyList<string> m = Mannschaften;
        return Form switch
        {
            Turnierform.Vierer => [(0, m[0], m[2]), (0, m[1], m[3]), (1, m[0], m[3]), (1, m[2], m[1])],
            Turnierform.Dreier => [(0, m[0], m[1]), (1, m[1], m[2]), (2, m[0], m[2])],
            _ => [(0, m[0], m[1])],
        };
    }
}
