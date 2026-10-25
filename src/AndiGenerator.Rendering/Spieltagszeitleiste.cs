// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>
/// Die Spieltage als Zeitleiste (Diagramm „Spieltage“): Spieltag k ist wie im Original das k-te Spiel jeder Mannschaft. Je
/// Spieltag die Wochen (Montag bis Sonntag) mit der Zahl der Mannschaften, seine Spanne und die Überschneidungen mit anderen
/// Spieltagen – an welchem Wochentag gespielt wird, spielt keine Rolle.
/// </summary>
public static class Spieltagszeitleiste
{
    /// <summary>Ermittelt die Zeilen der Zeitleiste.</summary>
    /// <param name="daten">Die Diagrammdaten.</param>
    /// <returns>Ein Spieltag je Zeile; Spieltage ohne terminiertes Spiel fehlen.</returns>
    public static IReadOnlyList<Spieltagszeile> Ermitteln(Diagrammdaten daten)
    {
        ArgumentNullException.ThrowIfNull(daten);
        List<List<Spieltagsmannschaft>> termine = daten.Mannschaften
            .Select(m => m.Spiele
                .Where(s => s.Zeitpunkt is not null)
                .Select(s => new Spieltagsmannschaft(m.Name, DateOnly.FromDateTime(s.Zeitpunkt!.Value), s.Heimspiel))
                .OrderBy(s => s.Tag)
                .ToList())
            .ToList();
        int anzahl = termine.Count == 0 ? 0 : termine.Max(t => t.Count);
        var roh = new List<(int Nummer, List<Spieltagstermin> Termine, bool WieVorher)>();
        for (int k = 0; k < anzahl; k++)
        {
            List<Spieltagstermin> tage = termine
                .Where(t => t.Count > k)
                .GroupBy(t => Montag(t[k].Tag), t => t[k])
                .OrderBy(g => g.Key)
                .Select(g => new Spieltagstermin(g.Key, g.OrderBy(s => s.Tag).ToList()))
                .ToList();
            if (tage.Count == 0)
            {
                continue;
            }

            List<List<Spieltagsmannschaft>> beide = termine.Where(t => t.Count > k && k > 0).ToList();
            bool wieVorher = beide.Count > 0 && beide.TrueForAll(t => t[k].Tag == t[k - 1].Tag);
            roh.Add((k + 1, tage, wieVorher));
        }

        var zeilen = new List<Spieltagszeile>();
        foreach ((int nummer, List<Spieltagstermin> tage, bool wieVorher) in roh)
        {
            DateOnly von = tage[0].Woche;
            DateOnly bis = tage[^1].Woche;
            var abschnitte = new List<Spieltagsabschnitt>();
            var mit = new List<int>();

            // Der Partner am selben Termin (Kompakt-, Doppelspieltag) gehört dazu und überschneidet sich nicht.
            foreach ((int andere, List<Spieltagstermin> andereTage, _) in roh.Where(r => r.Nummer != nummer && !Partner(r, nummer, wieVorher)))
            {
                DateOnly a = von > andereTage[0].Woche ? von : andereTage[0].Woche;
                DateOnly b = bis < andereTage[^1].Woche ? bis : andereTage[^1].Woche;
                if (a <= b)
                {
                    mit.Add(andere);
                    abschnitte.Add(new Spieltagsabschnitt(a, b));
                }
            }

            zeilen.Add(new Spieltagszeile(nummer, tage, Vereinigen(abschnitte), mit, wieVorher));
        }

        return zeilen;
    }

    /// <summary>
    /// Wie viele Spieltage in der Woche ab einem Montag laufen (ihre Spanne berührt die Woche); Spieltage am selben Termin
    /// wie der vorige zählen mit diesem einmal.
    /// </summary>
    /// <param name="zeilen">Die Zeilen der Zeitleiste.</param>
    /// <param name="montag">Montag der Woche.</param>
    /// <returns>Die Anzahl.</returns>
    public static int Gleichzeitig(IReadOnlyList<Spieltagszeile> zeilen, DateOnly montag)
    {
        ArgumentNullException.ThrowIfNull(zeilen);
        DateOnly sonntag = montag.AddDays(6);
        return zeilen.Count(z => !z.WieVorher && z.Von <= sonntag && z.Bis >= montag);
    }

    /// <summary>Fasst Nummern zu Bereichen zusammen: „1, 2, 5 – 8“.</summary>
    /// <param name="nummern">Aufsteigende Nummern.</param>
    /// <returns>Der Text.</returns>
    public static string Bereiche(IReadOnlyList<int> nummern)
    {
        ArgumentNullException.ThrowIfNull(nummern);
        var teile = new List<string>();
        int i = 0;
        while (i < nummern.Count)
        {
            int j = i;
            while (j + 1 < nummern.Count && nummern[j + 1] == nummern[j] + 1)
            {
                j++;
            }

            if (j - i >= 2)
            {
                teile.Add($"{nummern[i]} – {nummern[j]}");
            }
            else
            {
                teile.AddRange(nummern.Skip(i).Take(j - i + 1).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            i = j + 1;
        }

        return string.Join(", ", teile);
    }

    /// <summary>Montag der Woche eines Tages.</summary>
    /// <param name="tag">Der Tag.</param>
    /// <returns>Der Montag.</returns>
    public static DateOnly Montag(DateOnly tag) => tag.AddDays(-(((int)tag.DayOfWeek + 6) % 7));

    private static bool Partner((int Nummer, List<Spieltagstermin> Termine, bool WieVorher) andere, int nummer, bool wieVorher) =>
        (andere.Nummer == nummer - 1 && wieVorher) || (andere.Nummer == nummer + 1 && andere.WieVorher);

    private static List<Spieltagsabschnitt> Vereinigen(List<Spieltagsabschnitt> abschnitte)
    {
        var ergebnis = new List<Spieltagsabschnitt>();
        foreach (Spieltagsabschnitt a in abschnitte.OrderBy(a => a.Von))
        {
            if (ergebnis.Count > 0 && a.Von <= ergebnis[^1].Bis.AddDays(7))
            {
                Spieltagsabschnitt letzter = ergebnis[^1];
                ergebnis[^1] = letzter with { Bis = a.Bis > letzter.Bis ? a.Bis : letzter.Bis };
            }
            else
            {
                ergebnis.Add(a);
            }
        }

        return ergebnis;
    }
}
