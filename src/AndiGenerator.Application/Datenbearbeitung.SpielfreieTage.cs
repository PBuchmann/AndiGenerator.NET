// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Spielfreie Tage“ (Original <c>TDialogPanelFreeDays</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Liefert alle Tage mit mindestens einem Heimspielwunsch (Original <c>getAllPossibleDates</c>), sortiert.</summary>
    /// <returns>Die Tage.</returns>
    public IReadOnlyList<DateOnly> MoeglicheSpieltage() => MoeglicheSpieltageAus(Arbeitsstand);

    /// <summary>Liest die spielfreien Tage, soweit sie mögliche Spieltage sind (Original <c>DateListFromData</c>).</summary>
    /// <returns>Die Tage, sortiert.</returns>
    public IReadOnlyList<DateOnly> SpielfreieTage() => SpielfreieTageAus(Arbeitsstand);

    /// <summary>Liest die spielfreien Tage des click-TT-Stands.</summary>
    /// <returns>Die Tage oder <c>null</c> ohne Standard.</returns>
    public IReadOnlyList<DateOnly>? SpielfreieTageStandard() => standard is null ? null : SpielfreieTageAus(standard);

    /// <summary>
    /// Schreibt die spielfreien Tage (Original <c>FormToData</c>). Abweichend vom Original bleiben spielfreie Tage erhalten,
    /// an denen keine Mannschaft einen Heimspielwunsch hat (Befund #22: im Original verschwanden sie beim Speichern).
    /// </summary>
    /// <param name="tage">Die gewählten Tage aus <see cref="MoeglicheSpieltage"/>.</param>
    public void SpielfreieTageSpeichern(IEnumerable<DateOnly> tage)
    {
        ArgumentNullException.ThrowIfNull(tage);
        HashSet<DateOnly> moeglich = [.. MoeglicheSpieltage()];
        List<DateOnly> alle = Arbeitsstand.KinderMitNamen("nogameday")
            .Select(k => Datum(k.Lesen("date")))
            .Where(t => !moeglich.Contains(t))
            .Concat(tage)
            .Distinct()
            .Order()
            .ToList();
        Arbeitsstand.KinderEntfernen("nogameday");
        foreach (DateOnly tag in alle)
        {
            new DatenKnoten("nogameday", Arbeitsstand).Setzen("date", tag.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }
    }

    private static List<DateOnly> MoeglicheSpieltageAus(DatenKnoten plan) =>
        Teams(plan)
            .SelectMany(t => t.KinderMitNamen("homegameday"))
            .Select(k => DateOnly.FromDateTime(DelphiKompatibel.DatumZeitStreng(k.Lesen("datetime")) ?? DelphiKompatibel.DelphiNull))
            .Distinct()
            .Order()
            .ToList();

    private static List<DateOnly> SpielfreieTageAus(DatenKnoten plan)
    {
        HashSet<DateOnly> moeglich = [.. MoeglicheSpieltageAus(plan)];
        return plan.KinderMitNamen("nogameday").Select(k => Datum(k.Lesen("date"))).Where(moeglich.Contains).Order().ToList();
    }
}
