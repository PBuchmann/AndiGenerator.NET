// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Woher eine Ansicht ihren Plan bezieht (MIGRATIONSPLAN E15).</summary>
public enum PlanQuellenArt
{
    /// <summary>Bester Plan der laufenden Generierung; aktualisiert sich bei jeder Verbesserung.</summary>
    LaufendeGenerierung,

    /// <summary>In click-TT vorhandener Plan (bestehender Spielplan der Datei).</summary>
    ClickTtPlan,

    /// <summary>Gemerkter Plan aus dem Staffelordner.</summary>
    GemerkterPlan,
}
