// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Turniere;

/// <summary>Erkennt in einem Spielplan die Turniertage: je Tag die Gruppen von Mannschaften, die gegeneinander spielen.</summary>
public static class Turniererkennung
{
    /// <summary>Zerlegt die terminierten Spiele in Turniere.</summary>
    /// <param name="spiele">Spiele des Plans; Spiele ohne Termin werden übergangen.</param>
    /// <returns>Die Turniere nach Tag und erster Uhrzeit.</returns>
    public static IReadOnlyList<Turnier> Erkennen(IEnumerable<Spiel> spiele)
    {
        ArgumentNullException.ThrowIfNull(spiele);
        var ergebnis = new List<Turnier>();
        foreach (IGrouping<DateOnly, Spiel> tag in spiele.Where(s => s.Zeitpunkt is not null).GroupBy(s => DateOnly.FromDateTime(s.Zeitpunkt!.Value)))
        {
            foreach (List<Spiel> gruppe in Gruppen(tag.ToList()))
            {
                ergebnis.Add(Einordnen(tag.Key, gruppe));
            }
        }

        return ergebnis.OrderBy(t => t.Tag).ThenBy(t => t.Spiele[0].Zeitpunkt).ToList();
    }

    /// <summary>Paarung unabhängig vom Heimrecht.</summary>
    /// <param name="a">Eine Mannschaft.</param>
    /// <param name="b">Die andere Mannschaft.</param>
    /// <returns>Die beiden Namen in fester Reihenfolge.</returns>
    public static (string Erste, string Zweite) Paar(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    /// <summary>Gruppen von Spielen, deren Mannschaften über Spiele miteinander verbunden sind.</summary>
    private static IEnumerable<List<Spiel>> Gruppen(List<Spiel> spiele)
    {
        var vertreter = new Dictionary<string, string>(StringComparer.Ordinal);
        string Wurzel(string mannschaft)
        {
            string m = mannschaft;
            while (vertreter.TryGetValue(m, out string? v) && v != m)
            {
                m = v;
            }

            return m;
        }

        foreach (Spiel spiel in spiele)
        {
            vertreter.TryAdd(spiel.Heim, spiel.Heim);
            vertreter.TryAdd(spiel.Gast, spiel.Gast);
            vertreter[Wurzel(spiel.Heim)] = Wurzel(spiel.Gast);
        }

        return spiele.GroupBy(s => Wurzel(s.Heim), StringComparer.Ordinal).Select(g => g.ToList());
    }

    private static Turnier Einordnen(DateOnly tag, List<Spiel> gruppe)
    {
        List<Spiel> spiele = gruppe.OrderBy(s => s.Zeitpunkt).ThenBy(s => s.Heim, StringComparer.Ordinal).ToList();
        List<string> mannschaften = spiele.SelectMany(s => new[] { s.Heim, s.Gast }).Distinct(StringComparer.Ordinal).ToList();
        var heimspiele = mannschaften.ToDictionary(m => m, m => spiele.Count(s => s.Heim == m), StringComparer.Ordinal);
        bool jederZweimal = mannschaften.TrueForAll(m => spiele.Count(s => s.Heim == m || s.Gast == m) == 2);
        bool paareEinzeln = spiele.Select(s => Paar(s.Heim, s.Gast)).Distinct().Count() == spiele.Count;
        Turnierform form = (mannschaften.Count, spiele.Count, jederZweimal && paareEinzeln) switch
        {
            (2, 1, _) => Turnierform.Einzel,
            (3, 3, true) => Turnierform.Dreier,
            (4, 4, true) => Turnierform.Vierer,
            _ => Turnierform.Unbekannt,
        };
        List<string> ausrichter = form == Turnierform.Einzel ? [spiele[0].Heim] : mannschaften.Where(m => heimspiele[m] == 2).ToList();
        return new Turnier(tag, form, mannschaften, spiele, ausrichter.Count > 0 ? ausrichter : mannschaften);
    }
}
