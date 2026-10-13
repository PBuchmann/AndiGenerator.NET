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

    /// <summary>
    /// Die Einstellungen des Kompaktplaners: Standard, mit dem Löser HiGHS für die gezielte Konstruktion, wenn er sich
    /// laden lässt.
    /// </summary>
    /// <returns>Die Einstellungen.</returns>
    public static Kompaktplanoptionen Optionen() =>
        Kompaktplanoptionen.Standard with { Loeser = Highsbruecke.Verfuegbar ? new Highsbruecke() : null };

    /// <summary>Stellt die Aufgabe für den Kompaktplaner zusammen: Spieltermine der Halbrunde (Sonntage, Samstage und Wochentage mit Heimtermin), Meldungen, Luftlinie, Spitze und Keller.</summary>
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
        List<DateOnly> tage = Spieltermine(staffel, turniere, Rueckrunde(staffel, planung), heute, daten.Kompakttage());
        IReadOnlyDictionary<string, string> bemerkungen = daten.Bemerkungen();
        List<Turniermeldung> meldungen = staffel.Mannschaften
            .Select(m => Meldung(m, tage, bemerkungen.GetValueOrDefault(m.Name, string.Empty), []))
            .OfType<Turniermeldung>()
            .ToList();
        IReadOnlyDictionary<string, Koordinate> lagen = daten.Hallenkoordinaten();
        double Entfernung(string a, string b) =>
            lagen.TryGetValue(a, out Koordinate x) && lagen.TryGetValue(b, out Koordinate y) ? x.Luftlinie(y) : 0;
        List<string> namen = staffel.Mannschaften.Select(m => m.Name).ToList();
        (IReadOnlyList<string> spitze, IReadOnlyList<string> keller, string? hinweis) = Staerkegruppen(namen, daten.Mannschaftsstaerken());
        return new Kompaktaufgabe(namen, tage, meldungen, Entfernung)
        {
            Spitze = spitze,
            Keller = keller,
            Hinweise = hinweis is null ? [] : [hinweis],
        };
    }

    /// <summary>
    /// Spitzen- und Abstiegsgruppe nach dem QTTR-Schnitt (Mannschaftsaufstellung): die vier stärksten (stärkste zuerst) und
    /// die vier schwächsten (schwächste zuerst), bei 6 oder 7 Mannschaften je drei (für 3er-Spieltage). Nur, wenn für alle
    /// ein Schnitt bekannt ist.
    /// </summary>
    /// <param name="namen">Die Mannschaften.</param>
    /// <param name="staerken">QTTR-Schnitt je Mannschaft.</param>
    /// <returns>Spitze, Keller (leer, wenn nicht möglich) und ggf. ein Hinweis.</returns>
    public static (IReadOnlyList<string> Spitze, IReadOnlyList<string> Keller, string? Hinweis) Staerkegruppen(
        IReadOnlyList<string> namen,
        IReadOnlyDictionary<string, Mannschaftsstaerke> staerken)
    {
        ArgumentNullException.ThrowIfNull(namen);
        ArgumentNullException.ThrowIfNull(staerken);
        int groesse = 0;
        if (namen.Count >= 8)
        {
            groesse = 4;
        }
        else if (namen.Count >= 6)
        {
            groesse = 3;
        }

        if (groesse == 0)
        {
            return ([], [], null);
        }

        List<string> ohne = namen.Where(n => !staerken.TryGetValue(n, out Mannschaftsstaerke? st) || !st.HatAufstellung || st.Schnitt <= 0).ToList();
        if (ohne.Count > 0)
        {
            return ([], [], "Spitzen- und Abstiegsspieltag: Für " + string.Join(", ", ohne) + " fehlt der QTTR-Schnitt (Mannschaftsaufstellung in der Einrichtung laden).");
        }

        List<string> sortiert = namen.OrderByDescending(n => staerken[n].Schnitt).ThenBy(n => n, StringComparer.Ordinal).ToList();
        return (sortiert.Take(groesse).ToList(), Enumerable.Reverse(sortiert).Take(groesse).ToList(), null);
    }

    /// <summary>
    /// Die Spiele eines geplanten Spieltags: Beginn zur eingetragenen Uhrzeit des Heimtermins des Ausrichters, ohne Uhrzeit
    /// (00:00) um 10 Uhr beim 3er-, um 11 Uhr beim 4er-Spieltag und bei der Einzelbegegnung; Runden im Abstand von drei Stunden. Spiellokal nur bei
    /// den Spielen des Ausrichters (das des Heimtermins, sonst das der Mannschaft, sonst „1“ – so ist der Ausrichter auch in
    /// einem gespeicherten Plan erkennbar); die übrigen werden in click-TT in seine Halle verlegt.
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

        // Ohne Uhrzeit (00:00) gilt der Standardbeginn; eine eingetragene Uhrzeit gilt so, wie sie ist (Peter, 13.10.2026).
        TimeSpan beginn = termin is not null && termin.Zeitpunkt.TimeOfDay > TimeSpan.Zero ? termin.Zeitpunkt.TimeOfDay : standard;
        DateTime start = turnier.Tag.ToDateTime(TimeOnly.MinValue) + beginn;
        string lokal = new[] { termin?.Spiellokal, staffel.Mannschaften.FirstOrDefault(m => m.Name == turnier.Ausrichter)?.Spiellokal }
            .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? "1";
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
}
