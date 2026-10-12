// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Application;

/// <summary>Kompaktspieltage planen (nicht im Original): Aufgabe für den Kompaktplaner und Spiele eines geplanten Spieltags.</summary>
public static partial class Kompaktplanung
{
    /// <summary>Abstand der Runden eines Spieltags.</summary>
    private static readonly TimeSpan Rundenabstand = TimeSpan.FromHours(3);

    /// <summary>Stellt die Aufgabe für den Kompaktplaner zusammen: Sonntage der Halbrunde, Meldungen, Luftlinie, Festgehaltenes, Fahrtwünsche.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="daten">Arbeitskopie der Spielplandaten.</param>
    /// <param name="planung">Eingestellte Rundenplanung.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <returns>Die Aufgabe.</returns>
    public static Kompaktaufgabe Aufgabe(Staffel staffel, Datenbearbeitung daten, Rundenplanung planung, DateOnly heute)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(daten);
        IReadOnlyList<Turnier> turniere = Turniererkennung.Erkennen(staffel.BestehenderSpielplan);
        List<DateOnly> tage = Sonntage(staffel, turniere, Rueckrunde(staffel, planung), heute);
        IReadOnlyDictionary<string, string> bemerkungen = daten.Bemerkungen();
        List<Turniermeldung> meldungen = staffel.Mannschaften
            .Select(m => Meldung(m, tage, bemerkungen.GetValueOrDefault(m.Name, string.Empty), []))
            .OfType<Turniermeldung>()
            .ToList();
        IReadOnlyDictionary<string, Koordinate> lagen = daten.Hallenkoordinaten();
        double Entfernung(string a, string b) =>
            lagen.TryGetValue(a, out Koordinate x) && lagen.TryGetValue(b, out Koordinate y) ? x.Luftlinie(y) : 0;
        return new Kompaktaufgabe(
            staffel.Mannschaften.Select(m => m.Name).ToList(),
            tage,
            meldungen,
            Entfernung,
            daten.Festgehalten(),
            daten.Fahrtwuensche());
    }

    /// <summary>
    /// Die Spiele eines geplanten Spieltags: Beginn zur Uhrzeit des Heimtermins des Ausrichters (frühestens 10 Uhr beim
    /// 3er-, 11 Uhr beim 4er-Spieltag und bei der Einzelbegegnung), Runden im Abstand von drei Stunden. Spiellokal nur bei
    /// den Spielen des Ausrichters; die übrigen werden in click-TT in seine Halle verlegt.
    /// </summary>
    /// <param name="turnier">Der Spieltag.</param>
    /// <param name="staffel">Die Staffel (Heimtermine des Ausrichters).</param>
    /// <returns>Die Spiele.</returns>
    public static IReadOnlyList<Spiel> Spiele(Planturnier turnier, Staffel staffel)
    {
        ArgumentNullException.ThrowIfNull(turnier);
        ArgumentNullException.ThrowIfNull(staffel);
        Heimspieltermin? termin = staffel.Mannschaften
            .Where(m => m.Name == turnier.Ausrichter)
            .SelectMany(m => m.Heimspieltermine)
            .FirstOrDefault(t => DateOnly.FromDateTime(t.Zeitpunkt) == turnier.Tag);
        TimeSpan standard = turnier.Form == Turnierform.Dreier ? TimeSpan.FromHours(10) : TimeSpan.FromHours(11);
        TimeSpan beginn = termin is not null && termin.Zeitpunkt.TimeOfDay > standard ? termin.Zeitpunkt.TimeOfDay : standard;
        DateTime start = turnier.Tag.ToDateTime(TimeOnly.MinValue) + beginn;
        string lokal = termin?.Spiellokal ?? string.Empty;
        return turnier.Begegnungen()
            .Select(b => new Spiel(start + (Rundenabstand * b.Runde), b.Heim, b.Gast, b.Heim == turnier.Ausrichter ? lokal : string.Empty))
            .ToList();
    }

    /// <summary>Die Spiele aller geplanten Spieltage.</summary>
    /// <param name="turniere">Die Spieltage.</param>
    /// <param name="staffel">Die Staffel.</param>
    /// <returns>Die Spiele nach Spieltagen.</returns>
    public static IReadOnlyList<Spiel> Spiele(IEnumerable<Planturnier> turniere, Staffel staffel)
    {
        ArgumentNullException.ThrowIfNull(turniere);
        return turniere.SelectMany(t => Spiele(t, staffel)).ToList();
    }

    /// <summary>
    /// Ein angezeigter Spieltag als festhaltbarer Spieltag: Ausrichter zuerst, beim Vierer dann der Partner, der nicht gegen
    /// den Ausrichter spielt, und dessen beide Gegner.
    /// </summary>
    /// <param name="spieltag">Der Spieltag.</param>
    /// <returns>Der Spieltag oder <c>null</c>, wenn Form oder Ausrichter unklar sind.</returns>
    public static Planturnier? AlsPlanturnier(Kompaktspieltag spieltag)
    {
        ArgumentNullException.ThrowIfNull(spieltag);
        if (spieltag.Ausrichter is not string ausrichter || spieltag.Spiele.Count == 0)
        {
            return null;
        }

        if (spieltag.Form == Turnierform.Einzel)
        {
            Spiel spiel = spieltag.Spiele[0];
            return new Planturnier(spieltag.Tag, Turnierform.Einzel, [spiel.Heim, spiel.Gast]);
        }

        List<string> gegner = spieltag.Spiele
            .Where(s => s.Heim == ausrichter || s.Gast == ausrichter)
            .Select(s => s.Heim == ausrichter ? s.Gast : s.Heim)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        List<string> partner = spieltag.Mannschaften.Where(m => m != ausrichter && !gegner.Contains(m)).ToList();
        int anzahl = spieltag.Form == Turnierform.Vierer ? 4 : 3;
        List<string> mannschaften = [ausrichter, .. partner, .. gegner];
        return (spieltag.Form is Turnierform.Dreier or Turnierform.Vierer) && mannschaften.Count == anzahl
            ? new Planturnier(spieltag.Tag, spieltag.Form, mannschaften)
            : null;
    }
}
