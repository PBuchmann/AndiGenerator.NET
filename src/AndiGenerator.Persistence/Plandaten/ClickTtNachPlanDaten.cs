// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.ClickTt;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>
/// Überführt den click-TT-Export in den Datenbaum – Nachbildung von <c>TPlanData.LoadFromClickTTFile</c> (Version 26.7.1.0).
/// Das Ergebnis ist die Basis, auf die die <c>.modifications</c> angewendet werden.
/// </summary>
public static class ClickTtNachPlanDaten
{
    private static readonly TimeSpan KoppelSchwelle = new(16, 59, 0);
    private static readonly TimeSpan KoppelAbstand = TimeSpan.FromHours(4);

    /// <summary>Liest eine click-TT-Datei und erzeugt den Basisbaum.</summary>
    /// <param name="pfad">Pfad der click-TT-Exportdatei.</param>
    /// <returns>Der Wurzelknoten <c>plan</c>.</returns>
    public static DatenKnoten Laden(string pfad) => Erzeugen(ClickTtLeser.Lesen(pfad));

    /// <summary>Erzeugt den Basisbaum (<c>plan</c>) aus dem gelesenen Export.</summary>
    /// <param name="staffel">Gelesene click-TT-Staffel.</param>
    /// <returns>Der Wurzelknoten <c>plan</c>.</returns>
    public static DatenKnoten Erzeugen(ClickTtStaffel staffel)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        DateOnly rueckrunde = staffel.Rueckrundenbeginn
            ?? throw new ClickTtFormatException("Pflichtattribut 'mid' (Beginn der Rückrunde) fehlt.");

        var plan = new DatenKnoten("plan");
        plan.Setzen("gender", staffel.Geschlecht);
        plan.Setzen("mid", Datum(rueckrunde));
        plan.Setzen("from", Datum(staffel.Von));
        plan.Setzen("until", Datum(staffel.Bis));
        plan.Setzen("name", staffel.Name);
        plan.Setzen("id", staffel.Id);

        foreach (ClickTtMannschaft mannschaft in staffel.Mannschaften)
        {
            MannschaftAnlegen(plan, mannschaft);
        }

        if (staffel.VorgegebeneSpiele.Count > 0)
        {
            SpieleAnlegen(new DatenKnoten("predefinedgames", plan), staffel.VorgegebeneSpiele);
        }

        foreach (ClickTtZeitraum zeitraum in staffel.SpielfreieZeitraeume)
        {
            // Original: ohne Dublettenprüfung
            for (DateOnly tag = zeitraum.Von; tag <= zeitraum.Bis; tag = tag.AddDays(1))
            {
                new DatenKnoten("nogameday", plan).Setzen("date", Datum(tag));
            }
        }

        PflichtspieltageAnlegen(plan, staffel.Pflichtspieltage);

        if (staffel.BestehenderSpielplan.Count > 0)
        {
            SpieleAnlegen(new DatenKnoten("existingschedule", plan), staffel.BestehenderSpielplan);
        }

        return plan;
    }

    internal static string Datum(DateOnly tag) => tag.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    internal static string DatumZeit(DateTime zeitpunkt) => zeitpunkt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    private static void MannschaftAnlegen(DatenKnoten plan, ClickTtMannschaft mannschaft)
    {
        var team = new DatenKnoten("team", plan);
        team.Setzen("clubid", mannschaft.VereinsId);
        team.Setzen("teamid", mannschaft.Id);
        team.Setzen("location", string.Empty);
        team.Setzen("homerights", -1);
        team.Setzen("teamnumber", mannschaft.Nummer);
        if (mannschaft.Name.Length > 0)
        {
            team.Setzen("teamname", mannschaft.Name);
        }

        // Reihenfolge wie im Original: Heimtermine, Sperrzeiträume, Auswärtskoppel, 60-km-Regel. Im Export stehen sie gemischt,
        // die Reihenfolge der Kinder ist für Merge/Diff ohne Bedeutung, Dubletten werden je Typ geprüft).
        foreach (ClickTtHeimspieltermin termin in mannschaft.Heimspieltermine)
        {
            HeimterminAnlegen(team, termin);
        }

        foreach (ClickTtZeitraum sperre in mannschaft.Sperrzeitraeume)
        {
            for (DateOnly tag = sperre.Von; tag <= sperre.Bis; tag = tag.AddDays(1))
            {
                if (team.KindIndex(KnotenSchluessel.Von("nogameday", Datum(tag))) < 0)
                {
                    new DatenKnoten("nogameday", team).Setzen("date", Datum(tag));
                }
            }
        }

        foreach (ClickTtAuswaertskoppel koppel in mannschaft.Auswaertskoppeln)
        {
            var knoten = new DatenKnoten("roadcouple", team);
            knoten.Setzen("teamnamea", koppel.MannschaftA);
            knoten.Setzen("teamnameb", koppel.MannschaftB);
            knoten.Setzen("sameday", koppel.AmSelbenTag ? 0 : 1);
        }

        // Original: TStringList.IndexOf – ohne Beachtung der Groß-/Kleinschreibung. Die Bedingung wird je Gegner
        // erst beim Durchlaufen geprüft, sieht also bereits angelegte Knoten.
        foreach (string gegner in mannschaft.KeinWochenspielGegen.Where(g =>
            !team.KinderMitNamen("noweekgames").Any(k => string.Equals(k.Lesen("teamname"), g, StringComparison.OrdinalIgnoreCase))))
        {
            new DatenKnoten("noweekgames", team).Setzen("teamname", gegner);
        }

        if (!mannschaft.KeineDoppelspieltage)
        {
            DoppelspieltageMarkieren(team);
        }

        foreach (ClickTtNachbarmannschaft nachbar in mannschaft.Nachbarmannschaften)
        {
            NachbarmannschaftAnlegen(plan, team, nachbar);
        }
    }

    private static void HeimterminAnlegen(DatenKnoten team, ClickTtHeimspieltermin termin)
    {
        string zeitpunkt = DatumZeit(termin.Zeitpunkt);
        if (team.KindIndex(KnotenSchluessel.Von("homegameday", zeitpunkt)) >= 0)
        {
            return;
        }

        var knoten = new DatenKnoten("homegameday", team);
        knoten.Setzen("datetime", zeitpunkt);
        knoten.Setzen("couplegameday", false);
        knoten.Setzen("coupleauswaertssecondtime", false);
        knoten.Setzen("doublegameday", false);
        knoten.Setzen("ausweichtermin", termin.Ausweichtermin);
        knoten.Setzen("location", string.Empty);
        knoten.Setzen("parallelgames", termin.MaxParalleleSpiele);

        if (!string.IsNullOrEmpty(termin.Koppelwunsch))
        {
            knoten.Setzen("couplegameday", true);
            knoten.Setzen("coupleprio", termin.Koppelwunsch == "soft" ? 1 : 2);

            // Zweite Zeit: später Termin (nach 16:59) → 4 h früher, sonst 4 h später.
            TimeSpan zeit = termin.Zeitpunkt.TimeOfDay;
            TimeSpan zweite = zeit > KoppelSchwelle ? zeit - KoppelAbstand : zeit + KoppelAbstand;
            knoten.Setzen("couplesecondtime", TimeOnly.FromTimeSpan(zweite).ToString("HH:mm", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Zwei Heimtermine ohne Koppel-/Doppelmarkierung an aufeinanderfolgenden Tagen werden Doppelspieltage (Prio 1).
    /// Exakt wie im Original: Die Bedingung für den ersten Termin wird nur einmal vor der inneren Schleife geprüft,
    /// und es zählt nur „zweiter Termin genau 1 Tag nach dem ersten“.
    /// </summary>
    private static void DoppelspieltageMarkieren(DatenKnoten team)
    {
        List<DatenKnoten> tage = team.KinderMitNamen("homegameday").ToList();
        foreach (DatenKnoten tag1 in tage)
        {
            if (tag1.LesenBool("couplegameday") || tag1.LesenBool("doublegameday"))
            {
                continue;
            }

            foreach (DatenKnoten tag2 in tage)
            {
                if (ReferenceEquals(tag1, tag2) || tag2.LesenBool("couplegameday") || tag2.LesenBool("doublegameday"))
                {
                    continue;
                }

                if (TagAus(tag2).DayNumber - TagAus(tag1).DayNumber == 1)
                {
                    foreach (DatenKnoten t in new[] { tag1, tag2 })
                    {
                        t.Setzen("doublegameday", true);
                        t.Setzen("coupleprio", 1);
                    }
                }
            }
        }
    }

    private static void NachbarmannschaftAnlegen(DatenKnoten plan, DatenKnoten team, ClickTtNachbarmannschaft nachbar)
    {
        int index = team.KindIndex(KnotenSchluessel.Von("sisterteam", nachbar.Name, nachbar.Geschlecht));
        DatenKnoten knoten = index >= 0 ? team.Kinder[index] : new DatenKnoten("sisterteam", team);
        knoten.Setzen("teamname", nachbar.Name);
        knoten.Setzen("gender", nachbar.Geschlecht);
        knoten.Setzen("location", string.Empty);
        knoten.Setzen("teamnumber", nachbar.Nummer);
        knoten.Setzen("parallelhomegames", false);
        knoten.Setzen("noparallelgames", false);
        if (nachbar.Geschlecht == plan.Lesen("gender") && Math.Abs(team.LesenZahl("teamnumber") - knoten.LesenZahl("teamnumber")) == 1)
        {
            knoten.Setzen("noparallelgames", true); // echte Nachbarmannschaft: parallele Spiele vermeiden
        }

        foreach (ClickTtSpiel spiel in nachbar.Spiele)
        {
            if (spiel.Heim == "spielfrei" || spiel.Gast == "spielfrei")
            {
                continue;
            }

            string zeitpunkt = DatumZeit(spiel.Zeitpunkt);
            if (knoten.KindIndex(KnotenSchluessel.Von("sistergame", zeitpunkt)) >= 0)
            {
                continue; // Original: Duplikate derselben Zeit werden verworfen („Fehler mit den Bambinis“)
            }

            var spielKnoten = new DatenKnoten("sistergame", knoten);
            spielKnoten.Setzen("datetime", zeitpunkt);
            spielKnoten.Setzen("hometeamname", spiel.Heim);
            spielKnoten.Setzen("guestteamname", spiel.Gast);
        }
    }

    private static void SpieleAnlegen(DatenKnoten liste, IEnumerable<ClickTtSpiel> spiele)
    {
        foreach (ClickTtSpiel spiel in spiele)
        {
            var knoten = new DatenKnoten("game", liste);
            knoten.Setzen("datetime", DatumZeit(spiel.Zeitpunkt));
            knoten.Setzen("hometeamname", spiel.Heim);
            knoten.Setzen("guestteamname", spiel.Gast);
            knoten.Setzen("location", string.Empty);
        }
    }

    /// <summary>Aufeinanderfolgende Pflichtspieltage werden zu Bereichen mit je 1 Spiel zusammengefasst.</summary>
    private static void PflichtspieltageAnlegen(DatenKnoten plan, IEnumerable<DateOnly> tage)
    {
        DatenKnoten? bereich = null;
        DateOnly letzter = default;
        foreach (DateOnly tag in tage.Order())
        {
            if (bereich is not null && tag.DayNumber - letzter.DayNumber == 1)
            {
                bereich.Setzen("dateto", Datum(tag));
            }
            else
            {
                bereich = new DatenKnoten("mandatorygames", plan);
                bereich.Setzen("datefrom", Datum(tag));
                bereich.Setzen("dateto", Datum(tag));
                bereich.Setzen("numbergames", 1);
            }

            letzter = tag;
        }
    }

    private static DateOnly TagAus(DatenKnoten homegameday) =>
        DateOnly.ParseExact(homegameday.Lesen("datetime")[..10], "dd.MM.yyyy", CultureInfo.InvariantCulture);
}
