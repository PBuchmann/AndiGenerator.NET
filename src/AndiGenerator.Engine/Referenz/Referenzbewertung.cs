// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Bewertet einen Plan exakt wie das Original (Referenzmodell, 1:1-Portierung von <c>TPlan</c>/<c>TMannschaft</c>).
/// Dient als Maßstab für die schnelle Engine und für die Paritätstests; auf Geschwindigkeit ist es nicht ausgelegt.
/// </summary>
public static class Referenzbewertung
{
    /// <summary>Anzeigenamen der Kostenarten im Original (<c>cMannschaftsKostenTypeNamen</c>).</summary>
    public static IReadOnlyList<string> Kostenartnamen { get; } =
    [
        "Hallenbelegung", "parallele Spiele", "synchrone Heimsp.", "Sperrtermine", "Ausweichtermine", "60km Regel", "3-Tage-Abstand",
        "2-Spiele-die-Woche", "Spielverteilung", "Heimkoppel", "Auswärtskoppel", "Ungleich H/A", "Wechsel H/A", "Abstand H/A", "Setzliste",
        "Pflichtspieltage",
    ];

    /// <summary>Bewertet den bestehenden Spielplan der Staffel (wie „in Click-TT vorhandener Plan“ im Original).</summary>
    /// <param name="staffel">Staffel mit dem zu bewertenden Plan als bestehendem Spielplan.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <returns>Die Bewertung.</returns>
    public static Planbewertung Bewerten(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
        RefPlan.Spieltagskosten spieltag = plan.SpieltagsKosten();

        var mannschaften = new List<Mannschaftsbewertung>();
        foreach (RefMannschaft mannschaft in plan.Mannschaften)
        {
            var kosten = new RefMannschaftsKosten();
            mannschaft.KostenBerechnen(plan, kosten, meldungen: true);
            mannschaften.Add(new Mannschaftsbewertung(
                mannschaft.TeamName,
                kosten.Werte.Select(w => new Kostenzelle(w.Anzahl, w.AllCount, w.GesamtKosten)).ToList(),
                kosten.Gesamt(),
                kosten.Meldungen.ToList()));
        }

        return new Planbewertung(
            plan.Kosten(),
            plan.FehlterminKosten(),
            plan.NichtErlaubteKosten(),
            plan.SpielfreieTageKosten(),
            spieltag.Ueberlappung,
            spieltag.Breite,
            spieltag.LetzteUeberlappung,
            spieltag.LetzteBreite,
            plan.VereinsinterneSpieleAmAnfangKosten(),
            Enum.GetValues<MannschaftsKostenart>().Where(plan.HatWerteFuer).ToList(),
            mannschaften);
    }

    /// <summary>
    /// Automatisch ermittelte Enddaten der 1. und 3. Viertelrunde für die Doppelrunde (Original
    /// <c>getAutomaticMidDate1</c>/<c>getAutomaticMidDate2</c>: mittlerer Spieltagsmontag der Hin- bzw. Rückrunde).
    /// </summary>
    /// <param name="staffel">Staffel.</param>
    /// <param name="optionen">Berechnungsoptionen (die Rundenplanung wird berücksichtigt).</param>
    /// <returns>Die beiden Daten.</returns>
    public static (DateOnly Mitte1, DateOnly Mitte2) AutomatischeRundenmitten(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel with { BestehenderSpielplan = [] }, optionen with { AutomatischeMitte = true });
        return (DateOnly.FromDateTime(DelphiDatum.ZuDateTime(plan.Mitte1())), DateOnly.FromDateTime(DelphiDatum.ZuDateTime(plan.Mitte2())));
    }

    /// <summary>
    /// Meldungen einer Mannschaft zu einer einzelnen Kostenart (Original <c>CalculateKostenForType</c>, wie im Dialog
    /// „Gewichtung der Mannschaft … ändern“ beim Klick auf eine Zelle der Kostentabelle).
    /// </summary>
    /// <param name="staffel">Staffel mit dem zu bewertenden Plan als bestehendem Spielplan.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <param name="mannschaftsName">Name der Mannschaft.</param>
    /// <param name="art">Kostenart.</param>
    /// <returns>Die Meldungen; leer, wenn es die Mannschaft nicht gibt oder keine Meldung anfällt.</returns>
    public static IReadOnlyList<string> Meldungen(Staffel staffel, Berechnungsoptionen optionen, string mannschaftsName, MannschaftsKostenart art)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
        RefMannschaft? mannschaft = plan.Mannschaften.FirstOrDefault(m => m.TeamName == mannschaftsName);
        if (mannschaft is null)
        {
            return [];
        }

        var kosten = new RefMannschaftsKosten();
        mannschaft.KostenFuerArt(plan, art, kosten, meldungen: true);
        return kosten.Meldungen.ToList();
    }

    /// <summary>
    /// Meldungen aller Mannschaften zu einer einzelnen Kostenart (wie <see cref="Meldungen"/>, aber mit einmaligem Laden des
    /// Plans; für die Einzelheiten eines Kriteriums in der Qualitätsansicht).
    /// </summary>
    /// <param name="staffel">Staffel mit dem zu bewertenden Plan als bestehendem Spielplan.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <param name="art">Kostenart.</param>
    /// <returns>Je Mannschaft (in der Reihenfolge der Bewertung) ihre Meldungen.</returns>
    public static IReadOnlyList<Mannschaftsmeldungen> MeldungenJeMannschaft(Staffel staffel, Berechnungsoptionen optionen, MannschaftsKostenart art)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
        var liste = new List<Mannschaftsmeldungen>();
        foreach (RefMannschaft mannschaft in plan.Mannschaften)
        {
            var kosten = new RefMannschaftsKosten();
            mannschaft.KostenFuerArt(plan, art, kosten, meldungen: true);
            liste.Add(new Mannschaftsmeldungen(mannschaft.TeamName, kosten.Meldungen.ToList()));
        }

        return liste;
    }
}
