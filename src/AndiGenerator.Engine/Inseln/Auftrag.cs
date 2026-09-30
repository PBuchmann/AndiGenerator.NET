// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Kern;

namespace AndiGenerator.Engine.Inseln;

/// <summary>Neustart-Auftrag an einen Suchplatz: mit dieser Lösung weitermachen (Original <c>DoReInitPlan</c>/<c>DoReInitPlanData</c>).</summary>
/// <param name="Loesung">Neue Ausgangslösung; wird nicht verändert.</param>
/// <param name="Kosten">Ihre Kosten; −1 = unbekannt, dann gilt die nächste Lösung als Verbesserung.</param>
/// <param name="NeuerPlan">Neuer Plan mit anderen Gewichtungen (Spezial-Insel); aktiviert den Platz. <c>null</c> = Plan behalten.</param>
internal sealed record Auftrag(Loesung Loesung, double Kosten, KernPlan? NeuerPlan = null);
