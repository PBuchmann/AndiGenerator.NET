// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Spiele der Nachbarmannschaften“ (Original <c>TDialogPanelSisterTeamsDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Liest die Nachbarmannschaften einer Mannschaft (Original <c>DataToForm</c>), nach Bezeichnung sortiert.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Einträge.</returns>
    public IReadOnlyList<Nachbarmannschaftseintrag> Nachbarmannschaften(string mannschaft) =>
        Nachbarknoten(mannschaft)
            .Select((k, i) => new Nachbarmannschaftseintrag(i, Nachbareintragstext(k)))
            .OrderBy(e => e.Bezeichnung, Listenvergleich)
            .ToList();

    /// <summary>Öffnet eine Nachbarmannschaft zum Bearbeiten.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="index">Position der Nachbarmannschaft oder <c>null</c> für eine neue.</param>
    /// <returns>Der Entwurf.</returns>
    public Nachbarmannschaftsentwurf NachbarmannschaftBearbeiten(string mannschaft, int? index) =>
        new(index is int i ? Nachbarknoten(mannschaft).ElementAt(i) : null);

    /// <summary>
    /// Prüft einen Entwurf: Eingaben wie im Original, zusätzlich darf es dieselbe Nachbarmannschaft (Name und Art) bei der
    /// Mannschaft nicht zweimal geben, sonst ließe sich die Änderungsdatei nicht eindeutig schreiben.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="index">Position der bearbeiteten Nachbarmannschaft oder <c>null</c> für eine neue.</param>
    /// <param name="entwurf">Der Entwurf.</param>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public string? NachbarmannschaftPruefen(string mannschaft, int? index, Nachbarmannschaftsentwurf entwurf)
    {
        ArgumentNullException.ThrowIfNull(entwurf);
        if (entwurf.Pruefen() is string fehler)
        {
            return fehler;
        }

        bool doppelt = Nachbarknoten(mannschaft)
            .Where((_, i) => i != index)
            .Any(k => k.Lesen("teamname") == entwurf.Name.Trim() && k.Lesen("gender") == entwurf.Art.Trim());
        return doppelt ? "Diese Nachbarmannschaft gibt es schon" : null;
    }

    /// <summary>Übernimmt einen Entwurf (Original <c>GetValues</c>): neue Nachbarmannschaften werden angehängt.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="index">Position der bearbeiteten Nachbarmannschaft oder <c>null</c> für eine neue.</param>
    /// <param name="entwurf">Der Entwurf.</param>
    public void NachbarmannschaftUebernehmen(string mannschaft, int? index, Nachbarmannschaftsentwurf entwurf)
    {
        ArgumentNullException.ThrowIfNull(entwurf);
        DatenKnoten ziel = index is int i ? Nachbarknoten(mannschaft).ElementAt(i) : new DatenKnoten("sisterteam", Team(mannschaft));
        ziel.Zuweisen(entwurf.Abschliessen());
    }

    /// <summary>
    /// Sucht eine Nachbarmannschaft über ihre Bezeichnung auf der Spiellokal-Seite, z. B. <c>(Damen) TTC Beispiel</c>
    /// (Original <c>getSelectedSisterNode</c>: bei gleicher Bezeichnung die letzte).
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="bezeichnung">Bezeichnung „(Art) Name“.</param>
    /// <returns>Die Position oder <c>null</c>.</returns>
    public int? NachbarmannschaftIndex(string mannschaft, string bezeichnung)
    {
        int index = Nachbarknoten(mannschaft).Select(Nachbarbezeichnung).ToList().LastIndexOf(bezeichnung);
        return index >= 0 ? index : null;
    }

    /// <summary>Löscht eine Nachbarmannschaft.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="index">Position der Nachbarmannschaft.</param>
    public void NachbarmannschaftLoeschen(string mannschaft, int index) =>
        Team(mannschaft).Kinder.Remove(Nachbarknoten(mannschaft).ElementAt(index));

    /// <summary>Holt einen Wert, der angibt, ob die Nachbarmannschaften dem click-TT-Stand entsprechen (Original <c>isDefault</c>).</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns><c>true</c>, wenn gleich oder ohne Standard.</returns>
    public bool NachbarmannschaftenSindStandard(string mannschaft) =>
        standard is not DatenKnoten std || GleicheKinder(Team(mannschaft), Teams(std).FirstOrDefault(t => t.Lesen("teamname") == mannschaft), "sisterteam");

    /// <summary>Original <c>toDefault</c>: Nachbarmannschaften aus der click-TT-Datei übernehmen.</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    public void NachbarmannschaftenZuruecksetzen(string mannschaft)
    {
        DatenKnoten team = Team(mannschaft);
        team.KinderEntfernen("sisterteam");
        DatenKnoten? vorlage = standard is null ? null : Teams(standard).FirstOrDefault(t => t.Lesen("teamname") == mannschaft);
        foreach (DatenKnoten k in vorlage?.KinderMitNamen("sisterteam") ?? [])
        {
            new DatenKnoten("sisterteam", team).Zuweisen(k);
        }
    }

    /// <summary>Original <c>SisterTeamNodeToString</c>.</summary>
    private static string Nachbareintragstext(DatenKnoten k)
    {
        string text = $"({k.Lesen("gender")}) {k.Lesen("teamname")} {k.KinderMitNamen("sistergame").Count().ToString(CultureInfo.InvariantCulture)}, Spiele";
        if (k.Lesen("location").Length > 0)
        {
            text += ", Spiellokal: " + k.Lesen("location");
        }

        if (k.LesenBool("noparallelgames"))
        {
            text += ", gleichzeitige Spiele vermeiden";
        }

        if (k.LesenBool("parallelhomegames"))
        {
            text += ", gleichzeitige Heimspiele erwünscht";
        }

        return text;
    }

    private IEnumerable<DatenKnoten> Nachbarknoten(string mannschaft) => Team(mannschaft).KinderMitNamen("sisterteam");
}
