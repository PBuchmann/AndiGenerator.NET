// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Inseln;

/// <summary>Stufe eines Kriteriums im Automodus (E14): A muss, B soll, C ist wichtig und darf nicht geopfert werden.</summary>
public enum Stufe
{
    /// <summary>Stufe A: Kollisionen werden zuerst herausgedrängt.</summary>
    A,

    /// <summary>Stufe B: danach.</summary>
    B,

    /// <summary>Stufe C: wird bewacht; kein Kriterium darf aus dem Ruder laufen.</summary>
    C,
}
