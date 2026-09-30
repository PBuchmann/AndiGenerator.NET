// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Inhalt des Tabs „Termine der Nachbarmannschaften“ im Original (<c>PaintSisterTeams</c>): je Mannschaft die
/// gemeldeten Spiele ihrer Nachbarmannschaften aus anderen Staffeln, nach Tagen gruppiert. Hängt nicht vom Plan ab.
/// </summary>
public static class Nachbarterminauswertung
{
    /// <summary>Ermittelt die Nachbartermine aller Mannschaften der Staffel.</summary>
    /// <param name="staffel">Staffel.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <returns>Je Mannschaft (Reihenfolge des Originals) die Spiele der Nachbarmannschaften je Tag.</returns>
    public static IReadOnlyList<MannschaftsNachbartermine> Ermitteln(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel with { BestehenderSpielplan = [] }, optionen);
        return plan.Mannschaften.Select(Termine).ToList();
    }

    /// <summary>Original <c>TMapSisterGames.toSortedArray</c> und <c>TSisterGames.toSortedArray</c>: Tage und Spiele nach Termin.</summary>
    private static MannschaftsNachbartermine Termine(RefMannschaft mannschaft) => new(
        mannschaft.TeamName,
        mannschaft.Nachbarspiele
            .OrderBy(t => t.Key)
            .Select(t => (IReadOnlyList<Nachbartermin>)t.Value
                .OrderBy(n => n.Datum)
                .ThenBy(n => n.Mannschaftsname, StringComparer.Ordinal)
                .Select(n => new Nachbartermin(DelphiDatum.ZuDateTime(n.Datum), n.Geschlecht, n.Heim, n.Gast, n.Lokal, n.KeineParallelenSpiele))
                .ToList())
            .ToList());
}
