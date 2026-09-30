// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Presentation;

/// <summary>Auswählbare Planquelle einer Ansicht.</summary>
/// <param name="Art">Art der Quelle.</param>
/// <param name="Name">Anzeigename.</param>
/// <param name="Eintrag">Gemerkter Plan bei <see cref="PlanQuellenArt.GemerkterPlan"/>, sonst <c>null</c>.</param>
public sealed record PlanQuelle(PlanQuellenArt Art, string Name, GemerkterPlanEintrag? Eintrag)
{
    /// <summary>Holt die Quelle „laufende Generierung“.</summary>
    public static PlanQuelle Laufend { get; } = new(PlanQuellenArt.LaufendeGenerierung, "Laufende Generierung", null);

    /// <summary>Holt die Quelle „in click-TT vorhandener Plan“.</summary>
    public static PlanQuelle ClickTt { get; } = new(PlanQuellenArt.ClickTtPlan, "In click-TT vorhandener Plan", null);

    /// <summary>Erzeugt die Quelle zu einem gemerkten Plan.</summary>
    /// <param name="eintrag">Der gemerkte Plan.</param>
    /// <returns>Die Quelle.</returns>
    public static PlanQuelle Gemerkt(GemerkterPlanEintrag eintrag)
    {
        ArgumentNullException.ThrowIfNull(eintrag);
        return new PlanQuelle(PlanQuellenArt.GemerkterPlan, "Gemerkt: " + eintrag.Name, eintrag);
    }
}
