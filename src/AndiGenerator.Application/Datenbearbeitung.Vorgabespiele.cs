// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Manuell festgelegte Begegnungen“ (Original <c>TDialogPanelPredefinedGames</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>Holt einen Wert, der angibt, ob die festgelegten Begegnungen dem click-TT-Stand entsprechen (Original <c>isDefault</c>).</summary>
    public bool VorgegebeneSpieleSindStandard
    {
        get
        {
            if (standard is null)
            {
                return true;
            }

            DatenKnoten? jetzt = Arbeitsstand.ErstesKind("predefinedgames");
            DatenKnoten? vorlage = standard.ErstesKind("predefinedgames");
            return jetzt is null || vorlage is null ? ReferenceEquals(jetzt, vorlage) : jetzt.IstGleich(vorlage);
        }
    }

    /// <summary>Liest die festgelegten Begegnungen (Original <c>DataToForm</c>): nach Termin sortiert, Spiellokal bereinigt.</summary>
    /// <returns>Die Begegnungen.</returns>
    public IReadOnlyList<Vorgabespiel> VorgegebeneSpiele() =>
        Vorgabeknoten()
            .Select((k, i) => VorgabespielLesen(k, i) with { Spiellokal = Spiellokale.Korrigieren(k.Lesen("location")) })
            .OrderBy(s => s.Termin)
            .ToList();

    /// <summary>Liest eine Begegnung zum Bearbeiten.</summary>
    /// <param name="index">Position der Begegnung.</param>
    /// <returns>Die Begegnung mit dem gespeicherten Spiellokal.</returns>
    public Vorgabespiel VorgegebenesSpiel(int index) => VorgabespielLesen(Vorgabeknoten().ElementAt(index), index);

    /// <summary>
    /// Prüft eine Begegnung wie das Original (<c>ButtonOKClick</c>). Zusätzlich darf es dieselbe Begegnung zum selben Termin
    /// nicht zweimal geben, sonst ließe sich die Änderungsdatei nicht eindeutig schreiben.
    /// </summary>
    /// <param name="index">Position der bearbeiteten Begegnung oder <c>null</c> für eine neue.</param>
    /// <param name="spiel">Die neuen Werte.</param>
    /// <returns>Die Fehlermeldung oder <c>null</c>.</returns>
    public string? VorgegebenesSpielPruefen(int? index, Vorgabespiel spiel)
    {
        ArgumentNullException.ThrowIfNull(spiel);
        if (spiel.Heim.Length == 0)
        {
            return "Keine Heimmanschaft angegeben";
        }

        if (spiel.Gast.Length == 0)
        {
            return "Keine Gastmanschaft angegeben";
        }

        if (spiel.Heim == spiel.Gast)
        {
            return "Die Mannschaft kann nicht gegen sich selbst spielen";
        }

        string termin = DelphiKompatibel.DatumZeitSchreiben(spiel.Termin);
        bool doppelt = Vorgabeknoten()
            .Where((_, i) => i != index)
            .Any(k => k.Lesen("datetime") == termin && k.Lesen("hometeamname") == spiel.Heim && k.Lesen("guestteamname") == spiel.Gast);
        return doppelt ? "Dieses Spiel gibt es zu diesem Termin schon" : null;
    }

    /// <summary>Legt eine Begegnung an oder ändert sie (Original <c>GetValue</c>).</summary>
    /// <param name="index">Position der bearbeiteten Begegnung oder <c>null</c> für eine neue.</param>
    /// <param name="spiel">Die Werte.</param>
    public void VorgegebenesSpielSpeichern(int? index, Vorgabespiel spiel)
    {
        ArgumentNullException.ThrowIfNull(spiel);
        DatenKnoten k;
        if (index is int i)
        {
            k = Vorgabeknoten().ElementAt(i);
        }
        else
        {
            DatenKnoten liste = Arbeitsstand.ErstesKind("predefinedgames") ?? new DatenKnoten("predefinedgames", Arbeitsstand);
            k = new DatenKnoten("game", liste);
        }

        k.Setzen("datetime", DelphiKompatibel.DatumZeitSchreiben(spiel.Termin));
        k.Setzen("hometeamname", spiel.Heim);
        k.Setzen("guestteamname", spiel.Gast);
        k.Setzen("location", spiel.Spiellokal);
    }

    /// <summary>Löscht eine Begegnung.</summary>
    /// <param name="index">Position der Begegnung.</param>
    public void VorgegebenesSpielLoeschen(int index) =>
        Arbeitsstand.ErstesKind("predefinedgames")?.Kinder.Remove(Vorgabeknoten().ElementAt(index));

    /// <summary>Original <c>toDefault</c>: festgelegte Begegnungen aus der click-TT-Datei übernehmen.</summary>
    public void VorgegebeneSpieleZuruecksetzen()
    {
        if (standard is null)
        {
            return;
        }

        DatenKnoten? jetzt = Arbeitsstand.ErstesKind("predefinedgames");
        DatenKnoten? vorlage = standard.ErstesKind("predefinedgames");
        if (vorlage is null)
        {
            if (jetzt is not null)
            {
                Arbeitsstand.Kinder.Remove(jetzt);
            }

            return;
        }

        (jetzt ?? new DatenKnoten("predefinedgames", Arbeitsstand)).Zuweisen(vorlage);
    }

    private static Vorgabespiel VorgabespielLesen(DatenKnoten k, int index) => new(
        index,
        DelphiKompatibel.DatumZeitStreng(k.Lesen("datetime")) ?? DelphiKompatibel.DelphiNull,
        k.Lesen("hometeamname"),
        k.Lesen("guestteamname"),
        k.Lesen("location"));

    private IEnumerable<DatenKnoten> Vorgabeknoten() => Arbeitsstand.ErstesKind("predefinedgames")?.KinderMitNamen("game") ?? [];
}
