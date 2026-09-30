// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Heimspiel- und Sperrtermine (Original <c>TPlanData.GetHomeDays</c>/<c>SetHomeDays</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Liest die Heimspiel- und Sperrtermine einer Mannschaft wie <c>GetHomeDays</c>: Heimspieltermine an Sperrterminen
    /// entfallen, alles nach Termin sortiert.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Termine.</returns>
    public IReadOnlyList<Heimtag> Heimtage(string mannschaft) => HeimtageAus(Arbeitsstand, mannschaft);

    /// <summary>
    /// Schreibt die Heimspiel- und Sperrtermine einer Mannschaft wie <c>SetHomeDays</c>: alle bisherigen Knoten werden
    /// entfernt und mit allen Attributen neu angelegt.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="tage">Die Termine.</param>
    public void HeimtageSpeichern(string mannschaft, IEnumerable<Heimtag> tage)
    {
        ArgumentNullException.ThrowIfNull(tage);
        List<Heimtag> liste = tage.ToList();
        foreach (DatenKnoten team in Teams(Arbeitsstand).Where(t => t.Lesen("teamname") == mannschaft))
        {
            team.KinderEntfernen("homegameday");
            team.KinderEntfernen("nogameday");
            foreach (Heimtag h in liste)
            {
                HeimtagSchreiben(team, h);
            }
        }
    }

    /// <summary>Original <c>GetHomeDays</c> auf einem beliebigen Stand (Arbeitskopie oder click-TT-Import).</summary>
    private static List<Heimtag> HeimtageAus(DatenKnoten plan, string mannschaft)
    {
        var tage = new List<Heimtag>();
        foreach (DatenKnoten team in Teams(plan).Where(t => t.Lesen("teamname") == mannschaft))
        {
            var gesperrt = new HashSet<DateOnly>();
            foreach (DatenKnoten sperre in team.KinderMitNamen("nogameday"))
            {
                DateOnly tag = Datum(sperre.Lesen("date"));
                gesperrt.Add(tag);
                tage.Add(new Heimtag(tag.ToDateTime(TimeOnly.MinValue), true, false, false, 0, false, TimeOnly.MinValue, 0, false, string.Empty));
            }

            tage.AddRange(team.KinderMitNamen("homegameday")
                .Select(HeimtagLesen)
                .Where(h => !gesperrt.Contains(DateOnly.FromDateTime(h.Datum))));
        }

        return tage.OrderBy(h => h.Datum).ToList();
    }

    private static Heimtag HeimtagLesen(DatenKnoten knoten)
    {
        bool koppel = knoten.LesenBool("couplegameday");
        bool doppel = !koppel && knoten.LesenBool("doublegameday");
        bool auswaerts = !koppel && knoten.LesenBool("coupleauswaertssecondtime");
        TimeOnly zweitzeit = koppel || auswaerts ? Zeit(knoten.Lesen("couplesecondtime")) : TimeOnly.MinValue;
        return new Heimtag(
            DelphiKompatibel.DatumZeitStreng(knoten.Lesen("datetime")) ?? DelphiKompatibel.DelphiNull,
            false,
            koppel,
            doppel,
            koppel || doppel ? knoten.LesenZahl("coupleprio") : 0,
            auswaerts,
            zweitzeit,
            knoten.LesenZahl("parallelgames"),
            knoten.LesenBool("ausweichtermin"),
            Spiellokale.Korrigieren(knoten.Lesen("location")));
    }

    private static void HeimtagSchreiben(DatenKnoten team, Heimtag h)
    {
        if (h.Sperrtermin)
        {
            new DatenKnoten("nogameday", team).Setzen("date", h.Datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            return;
        }

        var knoten = new DatenKnoten("homegameday", team);
        knoten.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(h.Datum));
        knoten.Setzen("parallelgames", h.MaxParallel);
        knoten.Setzen("ausweichtermin", h.Ausweichtermin);
        knoten.Setzen("location", Spiellokale.Korrigieren(h.Spiellokal));
        knoten.Setzen("doublegameday", h.Doppeltermin);
        knoten.Setzen("couplegameday", h.Koppeltermin);
        knoten.Setzen("coupleauswaertssecondtime", h.AuswaertsKoppelZweitzeit);
        string zweitzeit = h.KoppelZweitzeit.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (h.Koppeltermin)
        {
            knoten.Setzen("couplesecondtime", zweitzeit);
        }

        if (h.Koppeltermin || h.Doppeltermin)
        {
            knoten.Setzen("coupleprio", h.KoppelPrio);
        }

        if (h.AuswaertsKoppelZweitzeit)
        {
            knoten.Setzen("couplesecondtime", zweitzeit);
        }
    }

    private static TimeOnly Zeit(string text) => text.Length == 0 ? TimeOnly.MinValue : DelphiKompatibel.TimeFromString(text);
}
