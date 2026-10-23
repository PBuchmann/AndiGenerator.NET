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

    /// <summary>Der beste Plan unverändert (bei später Annahme: die Plätze gehen von dort ihre eigenen Wege).</summary>
    Bester,

    /// <summary>
    /// Der beste Plan mit einem kleinen Stoß: einige zufällige Spiele neu verteilt, wie ein Suchschritt – alle Spiele behalten
    /// einen Termin.
    /// </summary>
    Stoss,

    /// <summary>
    /// Ein Plan der Teilneuplanung, den HiGHS im Modell für besser hielt, die Bewertung aber nicht (A und B gleich gut, anders
    /// verteilt); ohne einen solchen wie <see cref="Mannschaften"/>.
    /// </summary>
    Teilneuplanung,

    /// <summary>
    /// Ein anderer guter Plan, der nicht bester Plan wurde (HiGHS-Linie, B-Variante, Zwischenplan der Untergrenze, Teilneuplanung):
    /// in A so gut wie der beste, in B höchstens wenig schlechter, aber aus einer anderen Gegend. Ohne einen solchen ein
    /// <see cref="Stoss"/>.
    /// </summary>
    Anderer,
}
