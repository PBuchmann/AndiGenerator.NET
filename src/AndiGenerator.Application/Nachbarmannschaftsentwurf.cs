// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>
/// Arbeitskopie einer Nachbarmannschaft für den Dialog „Nachbarmannschaft“ (Original <c>TDialogEditSisterTeam</c>): Die
/// Begegnungen werden in der Kopie bearbeitet; erst OK übernimmt sie (<c>GetValues</c>). Wird die Mannschaft umbenannt,
/// ziehen ihre Begegnungen den neuen Namen nach.
/// </summary>
public sealed class Nachbarmannschaftsentwurf
{
    private readonly DatenKnoten kopie;

    /// <summary>Initialisiert den Entwurf.</summary>
    /// <param name="original">Bisherige Nachbarmannschaft oder <c>null</c> für eine neue.</param>
    internal Nachbarmannschaftsentwurf(DatenKnoten? original)
    {
        kopie = original?.Kopie() ?? new DatenKnoten("sisterteam");
        AlterName = kopie.Lesen("teamname");
        Name = AlterName;
        Nummer = kopie.LesenZahl("teamnumber");
        Art = kopie.Lesen("gender");
        Spiellokal = Mannschaftsdaten.Spiellokale.Contains(kopie.Lesen("location")) ? kopie.Lesen("location") : string.Empty;
        KeineParallelenSpiele = kopie.LesenBool("noparallelgames");
        ParalleleHeimspiele = kopie.LesenBool("parallelhomegames");
    }

    /// <summary>Holt oder setzt den Mannschaftsnamen.</summary>
    public string Name { get; set; }

    /// <summary>Holt oder setzt die Mannschaftsnummer.</summary>
    public int Nummer { get; set; }

    /// <summary>Holt oder setzt die Art (Herren/Damen …).</summary>
    public string Art { get; set; }

    /// <summary>Holt oder setzt das Spiellokal.</summary>
    public string Spiellokal { get; set; }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob gleichzeitige Spiele mit dieser Mannschaft vermieden werden sollen.</summary>
    public bool KeineParallelenSpiele { get; set; }

    /// <summary>Holt oder setzt einen Wert, der angibt, ob gleichzeitige Heimspiele mit dieser Mannschaft erwünscht sind.</summary>
    public bool ParalleleHeimspiele { get; set; }

    /// <summary>Holt den Namen beim Öffnen (Original <c>OldSisterTeamName</c>).</summary>
    public string AlterName { get; }

    /// <summary>
    /// Liefert die Begegnungen wie im Original (<c>DataToGrid</c>): der alte Name erscheint als aktueller Name; Heimspiele
    /// ohne eigenes Spiellokal zeigen das Spiellokal der Mannschaft; nach Termin sortiert.
    /// </summary>
    /// <returns>Die Zeilen.</returns>
    public IReadOnlyList<Nachbarspielzeile> Spiele()
    {
        string name = Name.Trim();
        return kopie.KinderMitNamen("sistergame")
            .Select((k, i) =>
            {
                string heim = k.Lesen("hometeamname") == AlterName ? name : k.Lesen("hometeamname");
                string gast = k.Lesen("guestteamname") == AlterName ? name : k.Lesen("guestteamname");
                string eigenes = Persistence.Plandaten.Spiellokale.Korrigieren(k.Lesen("location"));
                string lokal = eigenes.Length > 0 || heim != name ? eigenes : Spiellokal;
                return new Nachbarspielzeile(i, new Nachbarspieldaten(Termin(k), heim, gast, lokal));
            })
            .OrderBy(z => z.Spiel.Termin)
            .ToList();
    }

    /// <summary>Liefert die Begegnung zum Bearbeiten (Original <c>SetValue</c>).</summary>
    /// <param name="index">Position des Spiels.</param>
    /// <returns>Das Spiel mit dem gespeicherten Spiellokal.</returns>
    public Nachbarspieldaten Spiel(int index)
    {
        DatenKnoten k = SpielKnoten(index);
        return new Nachbarspieldaten(Termin(k), k.Lesen("hometeamname"), k.Lesen("guestteamname"), k.Lesen("location"));
    }

    /// <summary>
    /// Prüft eine Begegnung. Zusätzlich zum Original darf es keine zweite Begegnung zum selben Termin geben, sonst ließe sich
    /// die Änderungsdatei nicht eindeutig schreiben.
    /// </summary>
    /// <param name="index">Position des bearbeiteten Spiels oder <c>null</c> für ein neues.</param>
    /// <param name="spiel">Die neuen Werte.</param>
    /// <param name="gegner">Eingegebener Gegner.</param>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public string? SpielPruefen(int? index, Nachbarspieldaten spiel, string? gegner)
    {
        ArgumentNullException.ThrowIfNull(spiel);
        if ((gegner ?? string.Empty).Trim().Length == 0)
        {
            return "Kein Gegner angegeben";
        }

        string termin = DelphiKompatibel.DatumZeitSchreiben(spiel.Termin);
        bool doppelt = kopie.KinderMitNamen("sistergame")
            .Where((_, i) => i != index)
            .Any(k => k.Lesen("datetime") == termin);
        return doppelt ? "Zu diesem Termin gibt es schon eine Begegnung" : null;
    }

    /// <summary>Legt eine Begegnung an oder ändert sie (Original <c>TDialogEditOneSisterGame.GetValue</c>).</summary>
    /// <param name="index">Position des bearbeiteten Spiels oder <c>null</c> für ein neues.</param>
    /// <param name="spiel">Die Werte.</param>
    /// <param name="heimspiel">Heimspiel der Nachbarmannschaft (Reihenfolge der Attribute wie im Original).</param>
    public void SpielSpeichern(int? index, Nachbarspieldaten spiel, bool heimspiel)
    {
        ArgumentNullException.ThrowIfNull(spiel);
        DatenKnoten k = index is int i ? SpielKnoten(i) : new DatenKnoten("sistergame", kopie);
        k.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(spiel.Termin));
        if (heimspiel)
        {
            k.Setzen("hometeamname", spiel.Heim);
            k.Setzen("guestteamname", spiel.Gast);
        }
        else
        {
            k.Setzen("guestteamname", spiel.Gast);
            k.Setzen("hometeamname", spiel.Heim);
        }

        k.Setzen("location", spiel.Spiellokal);
    }

    /// <summary>Löscht eine Begegnung.</summary>
    /// <param name="index">Position des Spiels.</param>
    public void SpielLoeschen(int index) => kopie.Kinder.Remove(SpielKnoten(index));

    /// <summary>Prüft die Eingaben wie das Original (<c>ButtonOKClick</c>).</summary>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public string? Pruefen()
    {
        if (Name.Trim().Length == 0)
        {
            return "Der Name darf nicht leer sein";
        }

        return Art.Trim().Length == 0 ? "Die Art darf nicht leer sein" : null;
    }

    /// <summary>Überträgt die Eingaben in die Kopie (Original <c>GetValues</c>) und liefert sie.</summary>
    /// <returns>Der fertige Knoten.</returns>
    internal DatenKnoten Abschliessen()
    {
        string name = Name.Trim();
        kopie.Setzen("teamname", name);
        kopie.Setzen("teamnumber", Nummer);
        kopie.Setzen("gender", Art);
        kopie.Setzen("location", Spiellokal);
        kopie.Setzen("noparallelgames", KeineParallelenSpiele);
        kopie.Setzen("parallelhomegames", ParalleleHeimspiele);
        foreach (DatenKnoten k in kopie.KinderMitNamen("sistergame"))
        {
            if (k.Lesen("hometeamname") == AlterName)
            {
                k.Setzen("hometeamname", name);
            }

            if (k.Lesen("guestteamname") == AlterName)
            {
                k.Setzen("guestteamname", name);
            }
        }

        return kopie;
    }

    private static DateTime Termin(DatenKnoten k) => DelphiKompatibel.DatumZeitStreng(k.Lesen("datetime")) ?? DelphiKompatibel.DelphiNull;

    private DatenKnoten SpielKnoten(int index) => kopie.KinderMitNamen("sistergame").ElementAt(index);
}
