// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Ob zwei Heimspiele in derselben Halle stattfinden (Hallenbelegung).</summary>
internal static class Hallenvergleich
{
    /// <summary>
    /// Mit <see cref="Domain.Optionen.Korrekturen.HallenNachNamen"/> nach dem Hallennamen, wenn beide bekannt sind, sonst
    /// wie das Original nach der Spiellokal-Nummer.
    /// </summary>
    /// <param name="nachNamen">Korrektur eingeschaltet.</param>
    /// <param name="lokalA">Spiellokal des ersten Spiels.</param>
    /// <param name="halleA">Halle des ersten Spiels oder leer.</param>
    /// <param name="lokalB">Spiellokal des zweiten Spiels.</param>
    /// <param name="halleB">Halle des zweiten Spiels oder leer.</param>
    /// <returns><c>true</c> bei gleicher Halle.</returns>
    public static bool Gleich(bool nachNamen, string lokalA, string halleA, string lokalB, string halleB) =>
        nachNamen && halleA.Length > 0 && halleB.Length > 0
            ? string.Equals(halleA.Trim(), halleB.Trim(), StringComparison.OrdinalIgnoreCase)
            : lokalA == lokalB;
}
