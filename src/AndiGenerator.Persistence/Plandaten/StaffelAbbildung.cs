// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Plandaten;

/// <summary>
/// Übergang zwischen dem Datenbaum (<see cref="DatenKnoten"/>, Original <c>TPlanData</c>) und dem typisierten Fachmodell
/// (<see cref="Staffel"/>). Gelesen wird wie in <c>TPlan.load</c> / <c>TMannschaft.load</c>: fehlende Attribute ergeben
/// leere Texte, 0 bzw. <c>false</c>; Spiellokale werden wie mit <c>FixLocation</c> bereinigt; ein ungültiges Datum löst wie im
/// Original einen Fehler aus. Geschrieben wird in der Form, die der click-TT-Import und die Dialoge erzeugen.
/// Im Datenbaum gilt ein leeres Attribut wie ein fehlendes (so vergleicht auch das Original); der Vergleich mit
/// <see cref="DatenKnoten.IstGleich"/> ist daher nach Lesen und Zurückschreiben erfüllt.
/// </summary>
public static class StaffelAbbildung
{
    /// <summary>Attribut am Knoten <c>plan</c> mit der Einteilung der Kriterien (nicht im Original, dort ohne Wirkung).</summary>
    public const string KriterienAttribut = "kriterienstufen";

    /// <summary>Liest die Stammdaten aus dem Datenbaum.</summary>
    /// <param name="plan">Wurzelknoten <c>plan</c> (click-TT-Import, ggf. mit eingearbeiteten Änderungen).</param>
    /// <returns>Die Staffel.</returns>
    /// <exception cref="PlanDatenFormatException">Ein Datum oder eine Uhrzeit ist ungültig.</exception>
    public static Staffel AusPlanDaten(DatenKnoten plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            DatenKnoten? ranking = plan.ErstesKind("ranking");
            return new Staffel(
                Name: plan.Lesen("name"),
                Id: plan.Lesen("id"),
                Geschlecht: plan.Lesen("gender"),
                Beginn: DatumOderNull(plan.Lesen("from")),
                Ende: DatumOderNull(plan.Lesen("until")),
                Rueckrundenbeginn: DatumOderNull(plan.Lesen("mid")),
                Mannschaften: plan.KinderMitNamen("team").Select(MannschaftLesen).ToList(),
                VorgegebeneSpiele: SpieleLesen(plan.ErstesKind("predefinedgames")),
                SpielfreieTage: plan.KinderMitNamen("nogameday").Select(k => Datum(k.Lesen("date"))).ToList(),
                Pflichtspielzeitraeume: plan.KinderMitNamen("mandatorygames").Select(PflichtspielzeitraumLesen).ToList(),
                Setzliste: ranking is null ? null : SetzlisteLesen(ranking),
                BestehenderSpielplan: SpieleLesen(plan.ErstesKind("existingschedule")))
            {
                Kriterienstufen = plan.Lesen(KriterienAttribut),
            };
        }
        catch (FormatException ex)
        {
            throw new PlanDatenFormatException("Ungültiges Datum oder ungültige Uhrzeit in den Plandaten: " + ex.Message, ex);
        }
    }

    /// <summary>Erzeugt den Datenbaum aus den Stammdaten.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <returns>Der Wurzelknoten <c>plan</c>.</returns>
    public static DatenKnoten NachPlanDaten(Staffel staffel)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        var plan = new DatenKnoten("plan");
        plan.Setzen("gender", staffel.Geschlecht);
        plan.Setzen("mid", DatumText(staffel.Rueckrundenbeginn));
        plan.Setzen("from", DatumText(staffel.Beginn));
        plan.Setzen("until", DatumText(staffel.Ende));
        plan.Setzen("name", staffel.Name);
        plan.Setzen("id", staffel.Id);
        if (staffel.Kriterienstufen.Length > 0)
        {
            plan.Setzen(KriterienAttribut, staffel.Kriterienstufen);
        }

        foreach (Mannschaft mannschaft in staffel.Mannschaften)
        {
            MannschaftSchreiben(plan, mannschaft);
        }

        if (staffel.VorgegebeneSpiele.Count > 0)
        {
            SpieleSchreiben(new DatenKnoten("predefinedgames", plan), staffel.VorgegebeneSpiele);
        }

        foreach (DateOnly tag in staffel.SpielfreieTage)
        {
            new DatenKnoten("nogameday", plan).Setzen("date", DatumText(tag));
        }

        foreach (Pflichtspielzeitraum zeitraum in staffel.Pflichtspielzeitraeume)
        {
            var knoten = new DatenKnoten("mandatorygames", plan);
            knoten.Setzen("datefrom", DatumText(zeitraum.Von));
            knoten.Setzen("dateto", DatumText(zeitraum.Bis));
            knoten.Setzen("numbergames", zeitraum.AnzahlSpiele);
        }

        if (staffel.BestehenderSpielplan.Count > 0)
        {
            SpieleSchreiben(new DatenKnoten("existingschedule", plan), staffel.BestehenderSpielplan);
        }

        if (staffel.Setzliste is not null)
        {
            var ranking = new DatenKnoten("ranking", plan);
            ranking.Setzen("active", staffel.Setzliste.Aktiv);
            foreach (Setzlisteneintrag eintrag in staffel.Setzliste.Eintraege)
            {
                var team = new DatenKnoten("team", ranking);
                team.Setzen("teamname", eintrag.Mannschaft);
                team.Setzen("rankingindex", eintrag.Platz);
            }
        }

        return plan;
    }

    private static Mannschaft MannschaftLesen(DatenKnoten team) => new(
        Name: team.Lesen("teamname"),
        UrspruenglicherName: team.Lesen("orgteamname"),
        Id: team.Lesen("teamid"),
        VereinsId: team.Lesen("clubid"),
        Nummer: team.LesenZahl("teamnumber"),
        Spiellokal: Spiellokale.Korrigieren(team.Lesen("location")),
        HeimspieleHinrunde: team.LesenZahl("homerights"),
        Heimspieltermine: team.KinderMitNamen("homegameday").Select(HeimspielterminLesen).ToList(),
        Sperrtermine: team.KinderMitNamen("nogameday").Select(k => Datum(k.Lesen("date"))).ToList(),
        Auswaertskoppeln: team.KinderMitNamen("roadcouple").Select(AuswaertskoppelLesen).ToList(),
        Heimrechte: team.KinderMitNamen("homerights").Select(HeimrechtLesen).OfType<Heimrechtvorgabe>().ToList(),
        KeinWochenspielGegen: team.KinderMitNamen("noweekgames").Select(k => k.Lesen("teamname")).ToList(),
        Nachbarmannschaften: team.KinderMitNamen("sisterteam").Select(NachbarmannschaftLesen).ToList());

    private static Heimspieltermin HeimspielterminLesen(DatenKnoten knoten)
    {
        // Original loadHomeGame: couplegameday hat Vorrang vor doublegameday.
        Terminkoppelung koppelung = Terminkoppelung.Keine;
        if (knoten.LesenBool("couplegameday"))
        {
            koppelung = Terminkoppelung.Koppeltermin;
        }
        else if (knoten.LesenBool("doublegameday"))
        {
            koppelung = Terminkoppelung.Doppelspieltag;
        }

        string zweite = knoten.Lesen("couplesecondtime");
        return new Heimspieltermin(
            Zeitpunkt: ZeitpunktPflicht(knoten.Lesen("datetime")),
            MaxParalleleSpiele: knoten.LesenZahl("parallelgames"),
            Ausweichtermin: knoten.LesenBool("ausweichtermin"),
            Spiellokal: Spiellokale.Korrigieren(knoten.Lesen("location")),
            Koppelung: koppelung,
            Prioritaet: PrioritaetLesen(knoten),
            ZweiteUhrzeit: zweite.Length == 0 ? null : DelphiKompatibel.TimeFromString(zweite),
            ZweiteUhrzeitFuerAuswaertskoppel: knoten.LesenBool("coupleauswaertssecondtime"));
    }

    private static Koppelprioritaet PrioritaetLesen(DatenKnoten knoten) => knoten.LesenZahl("coupleprio") switch
    {
        1 => Koppelprioritaet.Weich,
        2 => Koppelprioritaet.Hart,
        _ => Koppelprioritaet.Moeglich,
    };

    private static Auswaertskoppel AuswaertskoppelLesen(DatenKnoten knoten)
    {
        Auswaertskoppelart art = knoten.LesenZahl("sameday") switch
        {
            0 => Auswaertskoppelart.AmSelbenTag,
            1 => Auswaertskoppelart.AnVerschiedenenTagen,
            _ => Auswaertskoppelart.Beliebig,
        };
        return new Auswaertskoppel(knoten.Lesen("teamnamea"), knoten.Lesen("teamnameb"), art);
    }

    private static Pflichtspielzeitraum PflichtspielzeitraumLesen(DatenKnoten knoten) =>
        new(Datum(knoten.Lesen("datefrom")), Datum(knoten.Lesen("dateto")), knoten.LesenZahl("numbergames"));

    private static Setzliste SetzlisteLesen(DatenKnoten ranking)
    {
        List<Setzlisteneintrag> eintraege = ranking.KinderMitNamen("team")
            .Select(k => new Setzlisteneintrag(k.Lesen("teamname"), k.LesenZahl("rankingindex")))
            .ToList();
        return new Setzliste(ranking.LesenBool("active"), eintraege);
    }

    /// <summary>Original <c>loadHomeRights</c>: nur die Werte 1 und 2 zählen, alles andere wird ignoriert.</summary>
    private static Heimrechtvorgabe? HeimrechtLesen(DatenKnoten knoten) => knoten.LesenZahl("homeright") switch
    {
        1 => new Heimrechtvorgabe(knoten.Lesen("teamname"), Runde.Hinrunde),
        2 => new Heimrechtvorgabe(knoten.Lesen("teamname"), Runde.Rueckrunde),
        _ => null,
    };

    private static Nachbarmannschaft NachbarmannschaftLesen(DatenKnoten knoten) => new(
        Name: knoten.Lesen("teamname"),
        Geschlecht: knoten.Lesen("gender"),
        Nummer: knoten.LesenZahl("teamnumber"),
        Spiellokal: Spiellokale.Korrigieren(knoten.Lesen("location")),
        KeineParallelenSpiele: knoten.LesenBool("noparallelgames"),
        ParalleleHeimspiele: knoten.LesenBool("parallelhomegames"),
        Spiele: knoten.KinderMitNamen("sistergame").Select(NachbarspielLesen).ToList());

    private static Nachbarspiel NachbarspielLesen(DatenKnoten knoten) => new(
        ZeitpunktPflicht(knoten.Lesen("datetime")),
        knoten.Lesen("hometeamname"),
        knoten.Lesen("guestteamname"),
        Spiellokale.Korrigieren(knoten.Lesen("location")));

    private static List<Spiel> SpieleLesen(DatenKnoten? liste) =>
        liste is null ? [] : liste.KinderMitNamen("game").Select(SpielLesen).ToList();

    private static Spiel SpielLesen(DatenKnoten knoten) => new(
        DelphiKompatibel.DatumZeitStreng(knoten.Lesen("datetime")),
        knoten.Lesen("hometeamname"),
        knoten.Lesen("guestteamname"),
        Spiellokale.Korrigieren(knoten.Lesen("location")));

    private static void MannschaftSchreiben(DatenKnoten plan, Mannschaft mannschaft)
    {
        var team = new DatenKnoten("team", plan);
        team.Setzen("clubid", mannschaft.VereinsId);
        team.Setzen("teamid", mannschaft.Id);
        team.Setzen("location", mannschaft.Spiellokal);
        team.Setzen("homerights", mannschaft.HeimspieleHinrunde);
        team.Setzen("teamnumber", mannschaft.Nummer);
        team.Setzen("teamname", mannschaft.Name);
        if (mannschaft.UrspruenglicherName.Length > 0)
        {
            team.Setzen("orgteamname", mannschaft.UrspruenglicherName);
        }

        foreach (Heimspieltermin termin in mannschaft.Heimspieltermine)
        {
            HeimspielterminSchreiben(team, termin);
        }

        foreach (DateOnly tag in mannschaft.Sperrtermine)
        {
            new DatenKnoten("nogameday", team).Setzen("date", DatumText(tag));
        }

        foreach (Auswaertskoppel koppel in mannschaft.Auswaertskoppeln)
        {
            var knoten = new DatenKnoten("roadcouple", team);
            knoten.Setzen("teamnamea", koppel.MannschaftA);
            knoten.Setzen("teamnameb", koppel.MannschaftB);
            knoten.Setzen("sameday", (int)koppel.Art);
        }

        foreach (Heimrechtvorgabe vorgabe in mannschaft.Heimrechte)
        {
            var knoten = new DatenKnoten("homerights", team);
            knoten.Setzen("teamname", vorgabe.Gegner);
            knoten.Setzen("homeright", (int)vorgabe.Runde);
        }

        foreach (string gegner in mannschaft.KeinWochenspielGegen)
        {
            new DatenKnoten("noweekgames", team).Setzen("teamname", gegner);
        }

        foreach (Nachbarmannschaft nachbar in mannschaft.Nachbarmannschaften)
        {
            NachbarmannschaftSchreiben(team, nachbar);
        }
    }

    /// <summary>Wie <c>TPlanData.SetHomeDays</c> und der click-TT-Import.</summary>
    private static void HeimspielterminSchreiben(DatenKnoten team, Heimspieltermin termin)
    {
        var knoten = new DatenKnoten("homegameday", team);
        knoten.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(termin.Zeitpunkt));
        knoten.Setzen("couplegameday", termin.Koppelung == Terminkoppelung.Koppeltermin);
        knoten.Setzen("coupleauswaertssecondtime", termin.ZweiteUhrzeitFuerAuswaertskoppel);
        knoten.Setzen("doublegameday", termin.Koppelung == Terminkoppelung.Doppelspieltag);
        knoten.Setzen("ausweichtermin", termin.Ausweichtermin);
        knoten.Setzen("location", termin.Spiellokal);
        knoten.Setzen("parallelgames", termin.MaxParalleleSpiele);
        if (termin.Koppelung != Terminkoppelung.Keine)
        {
            knoten.Setzen("coupleprio", (int)termin.Prioritaet);
        }

        if (termin.ZweiteUhrzeit is TimeOnly zweite)
        {
            knoten.Setzen("couplesecondtime", zweite.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static void NachbarmannschaftSchreiben(DatenKnoten team, Nachbarmannschaft nachbar)
    {
        var knoten = new DatenKnoten("sisterteam", team);
        knoten.Setzen("teamname", nachbar.Name);
        knoten.Setzen("gender", nachbar.Geschlecht);
        knoten.Setzen("location", nachbar.Spiellokal);
        knoten.Setzen("teamnumber", nachbar.Nummer);
        knoten.Setzen("parallelhomegames", nachbar.ParalleleHeimspiele);
        knoten.Setzen("noparallelgames", nachbar.KeineParallelenSpiele);
        foreach (Nachbarspiel spiel in nachbar.Spiele)
        {
            var game = new DatenKnoten("sistergame", knoten);
            game.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(spiel.Zeitpunkt));
            game.Setzen("hometeamname", spiel.Heim);
            game.Setzen("guestteamname", spiel.Gast);
            if (spiel.Spiellokal.Length > 0)
            {
                game.Setzen("location", spiel.Spiellokal);
            }
        }
    }

    private static void SpieleSchreiben(DatenKnoten liste, IEnumerable<Spiel> spiele)
    {
        foreach (Spiel spiel in spiele)
        {
            var game = new DatenKnoten("game", liste);
            game.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(spiel.Zeitpunkt));
            game.Setzen("hometeamname", spiel.Heim);
            game.Setzen("guestteamname", spiel.Gast);
            game.Setzen("location", spiel.Spiellokal);
        }
    }

    /// <summary>Original <c>GetAsDate</c>: leer ergibt den Nullpunkt 30.12.1899.</summary>
    private static DateOnly Datum(string text) =>
        text.Length == 0 ? DateOnly.FromDateTime(DelphiKompatibel.DelphiNull) : DelphiKompatibel.DateFromString(text);

    private static DateOnly? DatumOderNull(string text) => text.Length == 0 ? null : DelphiKompatibel.DateFromString(text);

    /// <summary>Original <c>GetAsDateTime</c> für Termine, die immer einen Wert haben: leer ergibt den Nullpunkt.</summary>
    private static DateTime ZeitpunktPflicht(string text) => DelphiKompatibel.DatumZeitStreng(text) ?? DelphiKompatibel.DelphiNull;

    private static string DatumText(DateOnly? datum) =>
        datum?.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
