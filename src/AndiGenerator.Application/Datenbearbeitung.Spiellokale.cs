// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Spiellokale (Kompaktansicht)“ (Original <c>TDialogPanelLocationsCompactDetail</c>).</summary>
public sealed partial class Datenbearbeitung
{
    private static readonly StringComparer Listenvergleich = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: true);

    /// <summary>Liest die Spiellokale einer Mannschaft (Original <c>DataToForm</c>).</summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <returns>Die Spiellokale; ungültige Angaben erscheinen leer.</returns>
    public Spiellokaldaten SpiellokaleLesen(string mannschaft)
    {
        DatenKnoten team = Team(mannschaft);
        return new Spiellokaldaten(
            Spiellokale.Korrigieren(team.Lesen("location")),
            Heimtage(mannschaft).Where(h => h.IstHeimtag).Select(h => (h.Datum, h.Spiellokal)).ToList(),
            team.KinderMitNamen("sisterteam")
                .Select(n => (Nachbarbezeichnung(n), Spiellokale.Korrigieren(n.Lesen("location"))))
                .OrderBy(n => n.Item1, Listenvergleich)
                .ToList());
    }

    /// <summary>
    /// Schreibt die Spiellokale einer Mannschaft (Original <c>FormToData</c>): Standardlokal, alle Heimspieltermine über
    /// <c>SetHomeDays</c> und die Nachbarmannschaften. Die Heimspieltermine werden in der Reihenfolge von
    /// <see cref="SpiellokaleLesen"/> zugeordnet, nicht wie im Original über das Datum (Befund #22: zwei Termine am selben Tag).
    /// </summary>
    /// <param name="mannschaft">Mannschaftsname.</param>
    /// <param name="daten">Die Spiellokale.</param>
    public void SpiellokaleSpeichern(string mannschaft, Spiellokaldaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        DatenKnoten team = Team(mannschaft);
        team.Setzen("location", daten.Standardlokal);

        var tage = new List<Heimtag>();
        int index = 0;
        foreach (Heimtag h in Heimtage(mannschaft))
        {
            if (h.IstHeimtag && index < daten.Heimtage.Count)
            {
                tage.Add(h with { Spiellokal = Spiellokale.Korrigieren(daten.Heimtage[index].Spiellokal) });
                index++;
            }
            else
            {
                tage.Add(h);
            }
        }

        HeimtageSpeichern(mannschaft, tage);

        foreach (DatenKnoten nachbar in team.KinderMitNamen("sisterteam"))
        {
            string bezeichnung = Nachbarbezeichnung(nachbar);
            string lokal = daten.Nachbarn.Where(n => n.Bezeichnung == bezeichnung).Select(n => n.Spiellokal).FirstOrDefault() ?? string.Empty;
            nachbar.Setzen("location", Spiellokale.Korrigieren(lokal));
        }
    }

    /// <summary>Original <c>SisterNodeToString</c>.</summary>
    private static string Nachbarbezeichnung(DatenKnoten nachbar) => $"({nachbar.Lesen("gender")}) {nachbar.Lesen("teamname")}";
}
