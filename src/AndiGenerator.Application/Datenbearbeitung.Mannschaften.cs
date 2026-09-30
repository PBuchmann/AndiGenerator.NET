// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Mannschaften“ (Original <c>TDialogPanelTeams</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Holt einen Wert, der angibt, ob die Mannschaften dem click-TT-Stand entsprechen (Original <c>isDefault</c>:
    /// jede Mannschaft hat eine gleiche im anderen Stand); ohne Standard immer <c>true</c>.
    /// </summary>
    public bool MannschaftenSindStandard => standard is not DatenKnoten std
        || (Teams(Arbeitsstand).All(a => Teams(std).Any(b => GleicheMannschaft(a, b)))
            && Teams(std).All(b => Teams(Arbeitsstand).Any(a => GleicheMannschaft(a, b))));

    /// <summary>Liefert die Namen aller Mannschaften, sortiert (Original <c>getTeamNames</c>).</summary>
    /// <returns>Die Namen.</returns>
    public IReadOnlyList<string> Mannschaftsnamen() =>
        Teams(Arbeitsstand).Select(t => t.Lesen("teamname")).Order(StringComparer.Ordinal).ToList();

    /// <summary>Liest die Stammdaten einer Mannschaft.</summary>
    /// <param name="name">Mannschaftsname.</param>
    /// <returns>Die Daten; das Spiellokal wie im Original bereinigt.</returns>
    public Mannschaftsdaten Mannschaft(string name)
    {
        DatenKnoten team = Team(name);
        return new Mannschaftsdaten(
            team.Lesen("teamname"),
            team.Lesen("teamid"),
            team.Lesen("clubid"),
            team.LesenZahl("teamnumber"),
            Spiellokale.Korrigieren(team.Lesen("location")));
    }

    /// <summary>
    /// Prüft, ob neue oder geänderte Stammdaten eindeutig sind (Original <c>ButtonTeamNewClick</c>/<c>ButtonTeamEditClick</c>).
    /// Abweichend vom Original wird die Mannschafts-ID mit den IDs verglichen, nicht mit den Namen (Befund #16).
    /// </summary>
    /// <param name="bisher">Name der bearbeiteten Mannschaft oder <c>null</c> für eine neue.</param>
    /// <param name="daten">Die neuen Daten.</param>
    /// <returns>Die erste Fehlermeldung oder <c>null</c>.</returns>
    public string? MannschaftPruefen(string? bisher, Mannschaftsdaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        if (daten.Pruefen() is string fehler)
        {
            return fehler;
        }

        Mannschaftsdaten d = daten.Bereinigt();
        DatenKnoten? eigene = bisher is null ? null : Team(bisher);
        foreach (DatenKnoten team in Teams(Arbeitsstand).Where(t => !ReferenceEquals(t, eigene)))
        {
            if (d.Name == team.Lesen("teamname"))
            {
                return "Mannschaftsname ist nicht eindeutig";
            }

            if (d.Id == team.Lesen("teamid"))
            {
                return "Mannschafts-ID ist nicht eindeutig";
            }

            if (d.Nummer == team.LesenZahl("teamnumber") && d.VereinsId == team.Lesen("clubid"))
            {
                return "Zu diesem Verein gibt es schon eine Mannschaft mit dieser Nummer";
            }
        }

        return null;
    }

    /// <summary>
    /// Legt eine Mannschaft an oder ändert sie wie das Original; bei einer Umbenennung bleibt der ursprüngliche Name in
    /// <c>orgteamname</c>. Abweichend vom Original werden Verweise auf den alten Namen (Setzliste, Heimrecht, 60 km,
    /// Auswärtskoppeln, vorgegebene Spiele) mit umbenannt (Befund #16).
    /// </summary>
    /// <param name="bisher">Name der bearbeiteten Mannschaft oder <c>null</c> für eine neue.</param>
    /// <param name="daten">Die neuen Daten (zuvor mit <see cref="MannschaftPruefen"/> geprüft).</param>
    public void MannschaftSpeichern(string? bisher, Mannschaftsdaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        Mannschaftsdaten d = daten.Bereinigt();
        DatenKnoten team;
        if (bisher is null)
        {
            team = new DatenKnoten("team", Arbeitsstand);
        }
        else
        {
            team = Team(bisher);
            string alt = team.Lesen("teamname");
            if (d.Name != alt)
            {
                if (d.Name == team.Lesen("orgteamname"))
                {
                    // Wieder in den ursprünglichen Namen umbenannt.
                    team.Setzen("orgteamname", string.Empty);
                }
                else if (team.Lesen("orgteamname").Length == 0)
                {
                    team.Setzen("orgteamname", alt);
                }

                VerweiseUmbenennen(alt, d.Name);
            }
        }

        team.Setzen("teamname", d.Name);
        team.Setzen("teamid", d.Id);
        team.Setzen("clubid", d.VereinsId);
        team.Setzen("location", d.Spiellokal);
        team.Setzen("teamnumber", d.Nummer);
        if (bisher is null)
        {
            // Befund #21: Wie beim click-TT-Import „Automatisch“ vorgeben; ohne Attribut plant die Engine 0 Heimspiele in der Vorrunde.
            team.Setzen("homerights", -1);
        }
    }

    /// <summary>Löscht eine Mannschaft (Original <c>ButtonTeamDeleteClick</c>).</summary>
    /// <param name="name">Mannschaftsname.</param>
    public void MannschaftLoeschen(string name) => Arbeitsstand.Kinder.Remove(Team(name));

    /// <summary>
    /// Original <c>TDialogPanelTeams.toDefault</c>: gelöschte Mannschaften aus dem click-TT-Stand wieder einfügen,
    /// hinzugefügte entfernen und die Attribute aller Mannschaften zurücksetzen (Zuordnung über den ursprünglichen Namen).
    /// </summary>
    public void MannschaftenZuruecksetzen()
    {
        if (standard is not DatenKnoten std)
        {
            return;
        }

        List<DatenKnoten> fehlend = Teams(std).Where(s => !Teams(Arbeitsstand).Any(a => GleichesTeam(a, s))).ToList();
        foreach (DatenKnoten team in fehlend)
        {
            new DatenKnoten("team", Arbeitsstand).Zuweisen(team);
        }

        List<DatenKnoten> hinzugefuegt = Teams(Arbeitsstand).Where(a => !Teams(std).Any(s => GleichesTeam(a, s))).ToList();
        foreach (DatenKnoten team in hinzugefuegt)
        {
            Arbeitsstand.Kinder.Remove(team);
        }

        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            foreach (DatenKnoten vorlage in Teams(std).Where(v => GleichesTeam(team, v)))
            {
                team.AttributeZuweisen(vorlage);
            }
        }
    }

    private static IEnumerable<DatenKnoten> Teams(DatenKnoten plan) => plan.KinderMitNamen("team");

    /// <summary>Original <c>isDefault.IsTheSame</c>.</summary>
    private static bool GleicheMannschaft(DatenKnoten a, DatenKnoten b) =>
        a.Lesen("teamname") == b.Lesen("teamname")
        && a.Lesen("teamid") == b.Lesen("teamid")
        && a.Lesen("location") == b.Lesen("location")
        && a.Lesen("clubid") == b.Lesen("clubid")
        && a.LesenZahl("teamnumber") == b.LesenZahl("teamnumber");

    /// <summary>Original <c>toDefault.IsTheSameTeam</c>: über den ursprünglichen Namen, falls umbenannt.</summary>
    private static bool GleichesTeam(DatenKnoten plan, DatenKnoten standardTeam)
    {
        string ursprung = plan.Lesen("orgteamname");
        return (ursprung.Length > 0 ? ursprung : plan.Lesen("teamname")) == standardTeam.Lesen("teamname");
    }

    private static void Umbenennen(IEnumerable<DatenKnoten> knoten, string attribut, string alt, string neu)
    {
        foreach (DatenKnoten k in knoten.Where(k => k.Lesen(attribut) == alt))
        {
            k.Setzen(attribut, neu);
        }
    }

    private DatenKnoten Team(string name) =>
        Teams(Arbeitsstand).FirstOrDefault(t => t.Lesen("teamname") == name)
        ?? throw new ArgumentException("Unbekannte Mannschaft: " + name, nameof(name));

    private void VerweiseUmbenennen(string alt, string neu)
    {
        foreach (DatenKnoten ranking in Arbeitsstand.KinderMitNamen("ranking"))
        {
            Umbenennen(ranking.KinderMitNamen("team"), "teamname", alt, neu);
        }

        foreach (DatenKnoten team in Teams(Arbeitsstand))
        {
            Umbenennen(team.KinderMitNamen("homerights"), "teamname", alt, neu);
            Umbenennen(team.KinderMitNamen("noweekgames"), "teamname", alt, neu);
            Umbenennen(team.KinderMitNamen("roadcouple"), "teamnamea", alt, neu);
            Umbenennen(team.KinderMitNamen("roadcouple"), "teamnameb", alt, neu);
        }

        foreach (DatenKnoten spiele in Arbeitsstand.KinderMitNamen("predefinedgames"))
        {
            Umbenennen(spiele.KinderMitNamen("game"), "hometeamname", alt, neu);
            Umbenennen(spiele.KinderMitNamen("game"), "guestteamname", alt, neu);
        }
    }
}
