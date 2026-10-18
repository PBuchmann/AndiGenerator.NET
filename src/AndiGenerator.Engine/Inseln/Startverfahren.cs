// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Womit eine Insel im Automodus neu beginnt, wenn sie feststeckt (reihum ein anderes): Jedes führt die Suche in eine andere
/// Gegend – vom leeren Plan weit weg, die übrigen in die Nähe des besten Plans, aber in ein anderes Tal. Die Stöße sind
/// bewusst klein: Muss eine Insel viele Spiele neu planen, setzt sie sie dorthin, wo gerade Platz ist, und hat danach viele
/// Verstöße in A und B, die sie lange nicht los wird.
/// </summary>
public enum Startverfahren
{
    /// <summary>Leerer Plan (wie im Original): ganz von vorn.</summary>
    Leer,

    /// <summary>Bester Plan, die Spiele einer zufälligen Mannschaft ohne Termin – die Insel plant sie neu.</summary>
    Mannschaften,

    /// <summary>Bester Plan, alle Spiele einer zufälligen Woche ohne Termin.</summary>
    Zeitfenster,

    /// <summary>Bester Plan, einige zufällige Spiele ohne Termin (ein kleiner Stoß).</summary>
    Spiele,

    /// <summary>
    /// Ein Plan der Teilneuplanung, den HiGHS im Modell für besser hielt, die Bewertung aber nicht (A und B gleich gut, anders
    /// verteilt); ohne einen solchen wie <see cref="Mannschaften"/>.
    /// </summary>
    Teilneuplanung,
}
