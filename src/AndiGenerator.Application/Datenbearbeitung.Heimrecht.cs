// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Heimrecht“ (Original <c>TDialogPanelHomeRightDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Liest das Heimrecht einer Mannschaft (Original <c>DataToForm</c>). Befund #21: Ein fehlendes Attribut
    /// <c>homerights</c> gilt als „Automatisch“ statt als „Vorrunde: 0“. Gegner, die nicht (mehr) in der Staffel sind,
    /// entfallen; gibt es für einen Gegner Vorrunde und Rückrunde, gewinnt wie im Original die Rückrunde.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Das Heimrecht; jeder andere Gegner ist enthalten, ohne Angabe mit „egal“.</returns>
    public Heimrecht HeimrechtLesen(string mannschaft)
    {
        DatenKnoten team = Team(mannschaft);
        string anzahl = team.Lesen("homerights");
        var gegner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in Mannschaftsnamen().Where(n => n != mannschaft))
        {
            gegner[name] = Heimrecht.Egal;
        }

        foreach (DatenKnoten k in team.KinderMitNamen("homerights"))
        {
            string name = k.Lesen("teamname");
            int wert = k.LesenZahl("homeright");
            if (gegner.TryGetValue(name, out int bisher) && (wert is Heimrecht.Vorrunde or Heimrecht.Rueckrunde) && bisher != Heimrecht.Rueckrunde)
            {
                gegner[name] = wert;
            }
        }

        return new Heimrecht(anzahl.Length == 0 ? Heimrecht.Automatisch : team.LesenZahl("homerights"), gegner);
    }

    /// <summary>
    /// Schreibt das Heimrecht (Original <c>FormToData</c>): die Anzahl und je Gegner mit Vorrunde oder Rückrunde einen
    /// Knoten; alle bisherigen Knoten werden ersetzt. Ein fehlendes Attribut bleibt bei „Automatisch“ fehlend, damit ohne
    /// Änderung nichts in die Änderungsdatei gelangt.
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="daten">Das Heimrecht.</param>
    public void HeimrechtSpeichern(string mannschaft, Heimrecht daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        DatenKnoten team = Team(mannschaft);
        if (team.Lesen("homerights").Length > 0 || daten.HeimspieleVorrunde != Heimrecht.Automatisch)
        {
            team.Setzen("homerights", daten.HeimspieleVorrunde);
        }

        team.KinderEntfernen("homerights");
        foreach (string name in Mannschaftsnamen())
        {
            if (daten.Gegner.TryGetValue(name, out int wert) && (wert is Heimrecht.Vorrunde or Heimrecht.Rueckrunde))
            {
                var knoten = new DatenKnoten("homerights", team);
                knoten.Setzen("teamname", name);
                knoten.Setzen("homeright", wert);
            }
        }
    }
}
