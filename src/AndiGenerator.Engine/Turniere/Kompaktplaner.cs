// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Plant eine Staffel mit Kompaktspieltagen (nicht im Original): sucht Pläne aus 3er- und 4er-Spieltagen (bei gerader
/// Mannschaftszahl mit je einer Einzelbegegnung), in denen jede Paarung genau einmal vorkommt, jede Mannschaft höchstens
/// einmal am Tag spielt und mindestens einen Spieltag ausrichtet, möglichst ohne Verstöße gegen die Meldungen. Daraus
/// werden Varianten gewählt: ausgewogen, kürzeste Fahrten (Luftlinie), gleichmäßiger Stand und gemeinsamer Spieltag.
/// Festgehaltene Spieltage und Fahrtwünsche gelten in jedem Plan.
/// </summary>
public static class Kompaktplaner
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Plant.</summary>
    /// <param name="aufgabe">Die Aufgabe.</param>
    /// <param name="optionen">Dauer, Threads und Startwert.</param>
    /// <param name="abbruch">Bricht die Suche ab; die bis dahin besten Pläne werden zurückgegeben.</param>
    /// <param name="fortschritt">Wird gelegentlich (aus einem Hintergrund-Thread) mit einem Text zum Stand aufgerufen.</param>
    /// <returns>Varianten und Hinweise.</returns>
    public static Kompaktergebnis Planen(Kompaktaufgabe aufgabe, Kompaktplanoptionen optionen, CancellationToken abbruch, Action<string>? fortschritt)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);
        ArgumentNullException.ThrowIfNull(optionen);
        var modell = new Kompaktmodell(aufgabe);
        var hinweise = new List<string>(modell.Hinweise);
        if (modell.N < 3 || modell.T == 0)
        {
            hinweise.Add("Zu wenige Mannschaften oder keine Spieltage – es gibt nichts zu planen.");
            return new Kompaktergebnis([], 0, hinweise);
        }

        var lauf = new Kompaktlauf(modell, optionen, abbruch, fortschritt);
        lauf.Ausfuehren();
        List<Kompaktloesung> front = lauf.Front.Alle();
        hinweise.AddRange(Viererhinweise(lauf.Baum));
        hinweise.AddRange(Ergebnishinweise(front, modell));
        List<Kompaktvariante> varianten = Kompaktauswahl.Varianten(front);

        // 3er-Spieltage nur zeigen, wenn sie kürzer sind als der kürzeste Plan nur aus 4er-Spieltagen (Peter, 13.10.2026).
        if (front.Count > 0 && front.TrueForAll(l => l.Dreier == 0) && lauf.Gemischt is Kompaktloesung gemischt && gemischt.Km < front.Min(l => l.Km) - 0.5)
        {
            varianten.Add(new Kompaktvariante("Kürzer mit 3er-Spieltagen", gemischt.Planturniere(), gemischt.Kennzahlen()));
        }

        return new Kompaktergebnis(varianten, lauf.Geprueft, hinweise);
    }

    private static List<string> Viererhinweise(Viererbaum? baum)
    {
        if (baum is null)
        {
            return [];
        }

        string knoten = baum.Knoten.ToString("N0", Deutsch);
        if (baum.Beste is not IReadOnlyList<Kompaktkandidat> beste)
        {
            return [baum.Vollstaendig
                ? $"Es gibt keinen Plan nur aus 4er-Spieltagen, der alle Meldungen einhält ({knoten} Teilpläne vollständig durchsucht)."
                : $"Die Suche nach Plänen nur aus 4er-Spieltagen wurde nach {knoten} Teilplänen abgebrochen, ohne einen zu finden."];
        }

        string km = beste.Sum(k => k.Km).ToString("N0", Deutsch);
        return [baum.Vollstaendig
            ? $"Alle Pläne nur aus 4er-Spieltagen vollständig durchsucht ({knoten} Teilpläne): Der kürzeste hat {km} km."
            : $"Pläne nur aus 4er-Spieltagen nicht vollständig durchsucht (Zeit abgelaufen, {knoten} Teilpläne): Der kürzeste gefundene hat {km} km."];
    }

    private static List<string> Ergebnishinweise(List<Kompaktloesung> front, Kompaktmodell modell)
    {
        if (front.Count == 0)
        {
            return ["Es wurde kein vollständiger Plan gefunden. Zu wenige mögliche Sonntage in den Meldungen, oder festgehaltene Spieltage bzw. Fahrtwünsche lassen keinen Plan zu."];
        }

        var liste = new List<string>();
        Kompaktloesung beste = front[0];
        if (beste.Verstoesse > 0)
        {
            liste.Add($"Ohne Verstöße gegen die Meldungen ließ sich kein Plan finden; die Varianten haben {beste.Verstoesse} Verstoß/Verstöße.");
        }

        if (beste.OhneHeimspieltag > 0)
        {
            string grund = beste.OhneHeimspieltag <= modell.MindestensOhneAusrichtung
                ? $"Bei {modell.N} Mannschaften gibt es dafür zu wenige Spieltage."
                : "Mit den Meldungen ließ sich das nicht anders lösen.";
            liste.Add($"{beste.OhneHeimspieltag} Mannschaft(en) richten keinen Spieltag aus. {grund}");
        }

        return liste;
    }
}
