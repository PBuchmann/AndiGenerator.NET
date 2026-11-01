// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Die Gewichte hinter den drei Stufen der <see cref="Kriterienwichtigkeit"/>.</summary>
public static class Kriterienwichtigkeiten
{
    /// <summary>Faktor für „nicht so wichtig“.</summary>
    public const double NiedrigFaktor = 0.15;

    /// <summary>Faktor für „besonders wichtig“.</summary>
    public const double HochFaktor = 6;

    /// <summary>Gibt den Gewichtsfaktor einer Stufe zurück.</summary>
    /// <param name="wichtigkeit">Die Stufe.</param>
    /// <returns>Der Faktor, mit dem das Kriteriumsgewicht multipliziert wird.</returns>
    public static double Faktor(Kriterienwichtigkeit wichtigkeit) => wichtigkeit switch
    {
        Kriterienwichtigkeit.Niedrig => NiedrigFaktor,
        Kriterienwichtigkeit.Hoch => HochFaktor,
        _ => 1,
    };
}
