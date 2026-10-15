// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Bei „nur Rückrunde“ (und Corona) liegt die Vorrunde fest: Sie kommt aus dem bestehenden Plan der Staffel (click-TT),
/// auch wenn die Generierung mit leerem oder gemerktem Plan beginnt. Sonst fehlte der Suche die Vorrunde, und sie dürfte
/// Paarungen in der Rückrunde in derselben Richtung wie in der Vorrunde ansetzen.
/// </summary>
public static class Vorrunde
{
    /// <summary>Der Ausgangsplan der Generierung: die feste Vorrunde der Staffel und die Rückrunde aus <paramref name="plan"/>.</summary>
    /// <param name="staffel">Die Staffel mit dem bestehenden Plan aus click-TT.</param>
    /// <param name="optionen">Die Optionen (Rundenplanung).</param>
    /// <param name="plan">Startplan (leer bzw. <c>null</c> = mit leerem Plan beginnen).</param>
    /// <returns>Der Plan, mit dem die Generierung beginnt.</returns>
    public static IReadOnlyList<Spiel> Festhalten(Staffel staffel, Berechnungsoptionen optionen, IReadOnlyList<Spiel>? plan)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        IReadOnlyList<Spiel> start = plan ?? [];
        if (optionen.Rundenplanung is not (Rundenplanung.NurRueckrunde or Rundenplanung.Corona))
        {
            return start;
        }

        RefPlan referenz = RefPlan.Laden(staffel with { BestehenderSpielplan = [] }, optionen);
        bool Fest(Spiel s) => s.Zeitpunkt is DateTime z && !referenz.IstInZuPlanenderRunde(DelphiDatum.Wert(z));
        List<Spiel> fest = staffel.BestehenderSpielplan.Where(Fest).ToList();
        return fest.Count == 0 ? start : [.. fest, .. start.Where(s => s.Zeitpunkt is not null && !Fest(s))];
    }
}
