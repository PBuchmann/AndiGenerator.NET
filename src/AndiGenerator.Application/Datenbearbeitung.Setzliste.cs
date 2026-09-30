// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Application;

/// <summary>Seite „Setzliste“ (Original <c>TDialogPanelRanking</c>).</summary>
public sealed partial class Datenbearbeitung
{
    /// <summary>
    /// Liest die Setzliste wie das Original (<c>DataToForm</c>): zuerst die gespeicherte Reihenfolge (nur vorhandene
    /// Mannschaften), dann alle übrigen Mannschaften alphabetisch.
    /// </summary>
    /// <returns>Die Setzliste.</returns>
    public Setzlistendaten Setzliste()
    {
        IReadOnlyList<string> namen = Mannschaftsnamen();
        var liste = new List<string>();
        bool aktiv = false;
        if (Arbeitsstand.ErstesKind("ranking") is DatenKnoten ranking)
        {
            aktiv = ranking.LesenBool("active");
            foreach (DatenKnoten team in ranking.KinderMitNamen("team"))
            {
                string name = team.Lesen("teamname");
                if (namen.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    // Wie im Original: bis zum gespeicherten Platz auffüllen (bei fortlaufenden Plätzen genau ein Eintrag).
                    int platz = team.LesenZahl("rankingindex");
                    while (liste.Count <= platz)
                    {
                        liste.Add(name);
                    }
                }
            }

            liste.RemoveAll(n => n.Length == 0);
        }

        liste.AddRange(namen.Where(n => !liste.Contains(n, StringComparer.Ordinal)).ToList());
        return new Setzlistendaten(aktiv, liste);
    }

    /// <summary>Schreibt die Setzliste wie das Original (<c>FormToData</c>): der Knoten wird neu aufgebaut.</summary>
    /// <param name="daten">Die Setzliste.</param>
    public void SetzlisteSpeichern(Setzlistendaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        DatenKnoten ranking = Arbeitsstand.ErstesKind("ranking") ?? new DatenKnoten("ranking", Arbeitsstand);
        ranking.AttributeZuweisen(new DatenKnoten("ranking"));
        ranking.Kinder.Clear();
        ranking.Setzen("active", daten.Aktiv);
        for (int i = 0; i < daten.Reihenfolge.Count; i++)
        {
            var team = new DatenKnoten("team", ranking);
            team.Setzen("teamname", daten.Reihenfolge[i]);
            team.Setzen("rankingindex", i);
        }
    }
}
