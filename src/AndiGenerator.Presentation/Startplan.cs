// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Plan, von dem aus eine Generierung startet.</summary>
/// <param name="Name">Anzeigename, z. B. <c>leerem Plan</c>.</param>
/// <param name="Quelle">Quelle des Ausgangsplans; <c>null</c> = leerer Plan wie im Original.</param>
public sealed record Startplan(string Name, PlanQuelle? Quelle)
{
    /// <summary>Holt den leeren Plan (Standard des Originals).</summary>
    public static Startplan Leer { get; } = new("leerem Plan", null);
}
