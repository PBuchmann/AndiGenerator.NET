// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TAuswaertsKoppel</c>.</summary>
internal sealed class RefAuswaertskoppel(string teamA, string teamB, AuswaertsKoppelTyp art)
{
    public string TeamA { get; } = teamA;

    public string TeamB { get; } = teamB;

    public AuswaertsKoppelTyp Art { get; } = art;

    public RefMannschaft? MannschaftA(RefPlan plan) => plan.FindeNachName(TeamA);

    public RefMannschaft? MannschaftB(RefPlan plan) => plan.FindeNachName(TeamB);
}
