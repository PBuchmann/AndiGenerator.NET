// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Presentation;

/// <summary>Die Terminwünsche einer Mannschaft als Karte.</summary>
/// <param name="Name">Name der Mannschaft.</param>
/// <param name="Status">Statusmarke: „Probleme“, „Halle belegt“, „genügend Termine“ oder leer.</param>
/// <param name="StatusFarbe">Farbe der Statusmarke.</param>
/// <param name="Abschnitte">Die Abschnitte der Karte.</param>
public sealed record Wunschkarte(string Name, string Status, Terminwunschfarbe StatusFarbe, IReadOnlyList<Wunschabschnitt> Abschnitte)
{
    /// <summary>Holt einen Wert, der angibt, ob es eine Statusmarke gibt.</summary>
    public bool HatStatus => Status.Length > 0;

    /// <summary>
    /// Bildet die Karten aus der Übersicht: Zeilen unter den Zwischenüberschriften des Originals gruppiert, Mannschaften mit
    /// Problemen (rote Zeilen) zuerst, sonst in der Reihenfolge des Originals.
    /// </summary>
    /// <param name="uebersicht">Die Übersicht oder <c>null</c>.</param>
    /// <returns>Die Karten.</returns>
    public static IReadOnlyList<Wunschkarte> Bilden(Terminwunschuebersicht? uebersicht)
    {
        if (uebersicht is null)
        {
            return [];
        }

        return uebersicht.Mannschaften
            .Select(Karte)
            .OrderBy(k => k.StatusFarbe == Terminwunschfarbe.Rot ? 0 : 1)
            .ToList();
    }

    private static Wunschkarte Karte(MannschaftsTerminwuensche m)
    {
        var abschnitte = new List<Wunschabschnitt>();
        string titel = string.Empty;
        var eintraege = new List<Wunscheintrag>();
        foreach (Terminwunschtext z in m.Zeilen)
        {
            if (z.Ueberschrift)
            {
                if (eintraege.Count > 0 || titel.Length > 0)
                {
                    abschnitte.Add(new Wunschabschnitt(titel, eintraege));
                }

                titel = z.Text.TrimEnd(':');
                eintraege = [];
            }
            else
            {
                eintraege.Add(new Wunscheintrag(z.Text, z.Farbe));
            }
        }

        if (eintraege.Count > 0 || titel.Length > 0)
        {
            abschnitte.Add(new Wunschabschnitt(titel, eintraege));
        }

        (string status, Terminwunschfarbe farbe) = m.Zeilen.Any(z => z.Farbe == Terminwunschfarbe.Rot)
            ? ("Probleme", Terminwunschfarbe.Rot)
            : Ohne(m);
        return new Wunschkarte(m.Name, status, farbe, abschnitte);
    }

    private static (string Status, Terminwunschfarbe Farbe) Ohne(MannschaftsTerminwuensche m)
    {
        if (m.Zeilen.Any(z => z.Farbe == Terminwunschfarbe.Gelb))
        {
            return ("Halle belegt", Terminwunschfarbe.Gelb);
        }

        return m.Zeilen.Any(z => z.Farbe == Terminwunschfarbe.Gruen) ? ("genügend Termine", Terminwunschfarbe.Gruen) : (string.Empty, Terminwunschfarbe.Normal);
    }
}
