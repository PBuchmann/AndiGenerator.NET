// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Pflichtspieltage“ (Original <c>TDialogPanelMandatoryDays</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Liest die Pflichtspielzeiträume (Original <c>DateListFromData</c>), sortiert nach Von, Bis und Anzahl.</summary>
    /// <returns>Die Zeiträume.</returns>
    public IReadOnlyList<Pflichtspielzeit> Pflichtspielzeiten() => PflichtspielzeitenAus(Arbeitsstand);

    /// <summary>Liest die Pflichtspielzeiträume des click-TT-Stands.</summary>
    /// <returns>Die Zeiträume oder <c>null</c> ohne Standard.</returns>
    public IReadOnlyList<Pflichtspielzeit>? PflichtspielzeitenStandard() => standard is null ? null : PflichtspielzeitenAus(standard);

    /// <summary>Schreibt die Pflichtspielzeiträume in der angegebenen Reihenfolge (Original <c>FormToData</c>).</summary>
    /// <param name="zeiten">Die Zeiträume.</param>
    public void PflichtspielzeitenSpeichern(IEnumerable<Pflichtspielzeit> zeiten)
    {
        ArgumentNullException.ThrowIfNull(zeiten);
        List<Pflichtspielzeit> liste = zeiten.ToList();
        Arbeitsstand.KinderEntfernen("mandatorygames");
        foreach (Pflichtspielzeit z in liste)
        {
            var knoten = new DatenKnoten("mandatorygames", Arbeitsstand);
            knoten.Setzen("datefrom", z.Von.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            knoten.Setzen("dateto", z.Bis.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            knoten.Setzen("numbergames", z.Anzahl);
        }
    }

    private static List<Pflichtspielzeit> PflichtspielzeitenAus(DatenKnoten plan) =>
        plan.KinderMitNamen("mandatorygames")
            .Select(k => new Pflichtspielzeit(Datum(k.Lesen("datefrom")), Datum(k.Lesen("dateto")), k.LesenZahl("numbergames")))
            .OrderBy(z => z.Von)
            .ThenBy(z => z.Bis)
            .ThenBy(z => z.Anzahl)
            .ToList();
}
