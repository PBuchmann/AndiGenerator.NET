// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Verteilt die Ausrichter über alle Turniere zugleich: zuerst möglichst wenige Gäste, die an dem Tag nicht auswärts
/// können, dann möglichst wenige Mannschaften ohne ausgerichtetes Turnier, dann möglichst gleichmäßig. Bis zu rund einer
/// Million Möglichkeiten wird vollständig gesucht, sonst Turnier für Turnier.
/// </summary>
internal static class Ausrichterverteilung
{
    private const double Grenze = 1 << 20;

    /// <summary>Wählt je Turnier einen Ausrichter.</summary>
    /// <param name="turniere">Die Turniere.</param>
    /// <param name="mannschaften">Alle Mannschaften der Staffel.</param>
    /// <param name="kann">Ob eine Mannschaft das Turnier ausrichten kann.</param>
    /// <param name="verstoesse">Zahl der Gäste, die bei diesem Ausrichter nicht auswärts können.</param>
    /// <returns>Je Turnier der Ausrichter oder <c>null</c>, wenn keiner der möglichen kann.</returns>
    public static string?[] Verteilen(
        IReadOnlyList<Turnier> turniere,
        IReadOnlyList<string> mannschaften,
        Func<Turnier, string, bool> kann,
        Func<Turnier, string?, int> verstoesse)
    {
        string?[][] optionen = turniere.Select(t => Optionen(t, kann)).ToArray();
        var suche = new Suche(turniere, mannschaften, optionen, verstoesse);
        if (optionen.Aggregate(1.0, (p, o) => p * o.Length) <= Grenze)
        {
            suche.Weiter(0, 0);
        }
        else
        {
            suche.Schrittweise();
        }

        return suche.Beste;
    }

    private static string?[] Optionen(Turnier turnier, Func<Turnier, string, bool> kann)
    {
        string?[] moeglich = turnier.MoeglicheAusrichter.Where(m => kann(turnier, m)).ToArray<string?>();
        return moeglich.Length > 0 ? moeglich : [null];
    }

    /// <summary>Stand der Suche: aktuelle Wahl, Zähler je Mannschaft und beste Verteilung.</summary>
    private sealed class Suche(
        IReadOnlyList<Turnier> turniere,
        IReadOnlyList<string> mannschaften,
        string?[][] optionen,
        Func<Turnier, string?, int> verstoesse)
    {
        private readonly string?[] aktuell = new string?[turniere.Count];
        private readonly Dictionary<string, int> zaehler = mannschaften.Distinct(StringComparer.Ordinal).ToDictionary(m => m, _ => 0, StringComparer.Ordinal);
        private (int Verstoesse, int OhneAusrichtung, int Streuung) besteWertung = (int.MaxValue, int.MaxValue, int.MaxValue);

        /// <summary>Holt die beste gefundene Verteilung.</summary>
        public string?[] Beste { get; } = new string?[turniere.Count];

        /// <summary>Vollständige Suche ab Turnier <paramref name="index"/>.</summary>
        /// <param name="index">Nächstes Turnier.</param>
        /// <param name="verstoesseBisher">Verstöße der Turniere vor <paramref name="index"/>.</param>
        public void Weiter(int index, int verstoesseBisher)
        {
            if (verstoesseBisher > besteWertung.Verstoesse)
            {
                return;
            }

            if (index == turniere.Count)
            {
                Abschliessen(verstoesseBisher);
                return;
            }

            Turnier turnier = turniere[index];
            foreach (string? ausrichter in optionen[index])
            {
                aktuell[index] = ausrichter;
                Zaehlen(turnier, ausrichter, 1);
                Weiter(index + 1, verstoesseBisher + verstoesse(turnier, ausrichter));
                Zaehlen(turnier, ausrichter, -1);
            }
        }

        /// <summary>Turnier für Turnier: wenigste Verstöße, dann bisher am seltensten ausgerichtet.</summary>
        public void Schrittweise()
        {
            for (int i = 0; i < turniere.Count; i++)
            {
                Turnier turnier = turniere[i];
                string? gewaehlt = optionen[i]
                    .OrderBy(a => verstoesse(turnier, a))
                    .ThenBy(a => a is null ? 0 : zaehler.GetValueOrDefault(a))
                    .First();
                Beste[i] = gewaehlt;
                Zaehlen(turnier, gewaehlt, 1);
            }
        }

        private void Zaehlen(Turnier turnier, string? ausrichter, int schritt)
        {
            if (ausrichter is not null && turnier.Form != Turnierform.Einzel)
            {
                zaehler[ausrichter] = zaehler.GetValueOrDefault(ausrichter) + schritt;
            }
        }

        private void Abschliessen(int verstoesseGesamt)
        {
            (int, int, int) wertung = (verstoesseGesamt, zaehler.Values.Count(n => n == 0), zaehler.Values.Sum(n => n * n));
            if (wertung.CompareTo(besteWertung) < 0)
            {
                besteWertung = wertung;
                aktuell.CopyTo(Beste, 0);
            }
        }
    }
}
