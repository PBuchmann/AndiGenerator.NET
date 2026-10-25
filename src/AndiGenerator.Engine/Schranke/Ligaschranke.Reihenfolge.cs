// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Kern;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Abstand Heim/Auswärts und Setzliste genau (nur für die Teilneuplanung): Beide zählen nach der Stelle eines Spiels in der Liste
/// der eigenen Spiele, die der <see cref="Spielzaehler"/> liefert. Ohne ihn bleiben die Näherungen.
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>
    /// Wie <c>KernPlan.Setzliste</c>: Das i-te Spiel (ab 0) einer Mannschaft in ihrer Runde gegen einen Gegner, der in der Setzliste a
    /// Plätze entfernt ist, verletzt sie, wenn i &gt; N − 1 − a (Nachbarn spielen zuletzt gegeneinander). Je Mannschaft und Spiel
    /// eine Ja/Nein-Spalte v, je Termin-Entscheidung x eine Zeile: g · v ≥ i − (N − 1 − a) − g · (1 − x), mit i = Stelle des Spiels
    /// − Spiele vor der Runde. <c>null</c> ohne Setzliste, bei Doppelrunde oder ohne <see cref="Spielzaehler"/>.
    /// </summary>
    private List<(int Spalte, double Faktor)>? SetzlisteGenau()
    {
        if (!d.HatSetzliste)
        {
            return [];
        }

        if (d.Doppelrunde || Zaehler() is not { } z)
        {
            return null;
        }

        int g = z.Spiele;
        int hoechsteStelle = d.N - 2;
        Dictionary<int, int> rundenanfang = termine.GroupBy(t => d.WunschRunde[t.Wunsch]).ToDictionary(gr => gr.Key, gr => gr.Min(t => d.WunschTag[t.Wunsch]));
        var verletzt = new Dictionary<(int Mannschaft, int Gruppe), int>();
        for (int i = 0; i < termine.Count; i++)
        {
            (int spalte, int spiel, int w) = termine[i];
            int heim = d.SpielHeim[spiel];
            int gast = d.SpielGast[spiel];
            foreach ((int t, int gegner) in new[] { (heim, gast), (gast, heim) })
            {
                int rang = d.SetzlistenRang[t];
                int gegnerRang = d.SetzlistenRang[gegner];
                int schwelle = d.N - 1 - Math.Abs(rang - gegnerRang);
                if (rang < 0 || gegnerRang < 0 || schwelle >= hoechsteStelle)
                {
                    continue;
                }

                if (!verletzt.TryGetValue((t, Gruppe(spiel)), out int v))
                {
                    v = modell.Spalte(0);
                    verletzt[(t, Gruppe(spiel))] = v;
                }

                // g · v − Stelle + vor der Runde − g · x ≥ Konstanten − Schwelle − g.
                (List<(int Spalte, double Faktor)> stelle, double k) = z.Stelle(t, i);
                (List<(int Spalte, double Faktor)> vorher, double kv) = z.BisAusdruck(t, rundenanfang[d.WunschRunde[w]] - 1);
                List<(int Spalte, double Faktor)> zeile = Zusammenfassen([(v, g), .. stelle.Select(e => (e.Spalte, -e.Faktor)), .. vorher, (spalte, -g)]);
                modell.Zeile(zeile, k - kv - schwelle - g, double.PositiveInfinity);
            }
        }

        return verletzt.Values.Select(v => (v, 1.0)).ToList();
    }

    /// <summary>
    /// Wie <c>KernPlan.AbstandHeimAuswaerts</c>: Je Mannschaft t und Gegner j der Abstand d(j) zwischen Hin- und Rückspiel in eigenen
    /// Spielen (Stelle des Spiels der Rückrunde − Stelle des Spiels der Hinrunde), daraus das Mittel über die n Gegner; gezählt
    /// wird die größte Abweichung vom Mittel, gerundet: z ≥ |d(j) − Mittel| − 1/2, genau (die Abweichungen sind Vielfache von
    /// 1/n): n · z ≥ |n · d(j) − Σ d| − (⌈n/2⌉ − 1); spielt t in jeder Halbrunde einmal gegen jeden, ist das Mittel n und
    /// z ≥ |d(j) − n|. Die Stelle eines Spiels der Runde r ist eine Zahl Q mit
    /// |Q − Stelle| ≤ g · (1 − x) je Termin-Entscheidung x; ein festes Spiel der Vorrunde hat seine feste Stelle. Nur für Hin- und
    /// Rückrunde (auch nur Rückrunde) ohne Doppelrunde und mit <see cref="Spielzaehler"/>; sonst <c>null</c>.
    /// </summary>
    private List<(int Spalte, double Faktor)>? AbstandGenau()
    {
        if (d.Rundenplanung is (Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde or Rundenplanung.Corona) && !d.Doppelrunde)
        {
            // Wie das Original: ohne Rückspiele keine Abstände, 0 Verstöße.
            return [];
        }

        if (d.Doppelrunde || d.Rundenplanung is not (Rundenplanung.Beide or Rundenplanung.NurRueckrunde) || Zaehler() is not { } z)
        {
            return null;
        }

        int g = z.Spiele;
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int t = 0; t < d.N; t++)
        {
            // Feste Spiele von t (Vorrunde) nach Datum: ihre Stelle steht fest.
            FesterTermin[] eigeneFeste = feste.Where(f => f.Heim == t || f.Gast == t).OrderBy(f => f.Datum).ToArray();
            var abstaende = new List<List<(int Spalte, double Faktor)>>();
            var konstanten = new List<double>();
            for (int j = 0; j < d.N; j++)
            {
                if (j == t)
                {
                    continue;
                }

                (List<(int Spalte, double Faktor)> E, double K)?[] stelle = [Stelle(t, j, 0, eigeneFeste, z), Stelle(t, j, 1, eigeneFeste, z)];
                if (stelle[0] is not { } hin || stelle[1] is not { } rueck)
                {
                    continue;
                }

                abstaende.Add(Zusammenfassen([.. rueck.E, .. hin.E.Select(e => (e.Spalte, -e.Faktor))]));
                konstanten.Add(rueck.K - hin.K);
            }

            int n = abstaende.Count;
            if (n <= 1)
            {
                continue;
            }

            int abweichung = modell.Zahl(0, g);
            ziel.Add((abweichung, 1.0));
            if (2 * n == g)
            {
                // Je Halbrunde ein Spiel gegen jeden Gegner: Dann ist Σ d = n · g / 2 fest, das Mittel n, und die Abweichung ganz –
                // z ≥ |d(j) − n| ohne die Summe (die alle Spiele der Mannschaft in jede Zeile holte; HiGHS war damit viel langsamer).
                for (int j = 0; j < n; j++)
                {
                    modell.Zeile([(abweichung, 1.0), .. abstaende[j].Select(e => (e.Spalte, -e.Faktor))], konstanten[j] - n, double.PositiveInfinity);
                    modell.Zeile([(abweichung, 1.0), .. abstaende[j]], n - konstanten[j], double.PositiveInfinity);
                }

                continue;
            }

            List<(int Spalte, double Faktor)> summe = Zusammenfassen(abstaende.SelectMany(a => a));
            double summeKonstant = konstanten.Sum();
            int spielraum = ((n + 1) / 2) - 1;
            for (int j = 0; j < n; j++)
            {
                // n · z − n · d(j) + Σ d ≥ −Spielraum und n · z + n · d(j) − Σ d ≥ −Spielraum (Konstanten nach rechts).
                double rest = (n * konstanten[j]) - summeKonstant;
                modell.Zeile(Zusammenfassen([(abweichung, n), .. abstaende[j].Select(e => (e.Spalte, -n * e.Faktor)), .. summe]), rest - spielraum, double.PositiveInfinity);
                modell.Zeile(Zusammenfassen([(abweichung, n), .. abstaende[j].Select(e => (e.Spalte, n * e.Faktor)), .. summe.Select(e => (e.Spalte, -e.Faktor))]), -rest - spielraum, double.PositiveInfinity);
            }
        }

        return ziel;
    }

    /// <summary>
    /// Die Stelle des Spiels von t gegen j in Runde r als Ausdruck: fest (Vorrunde bei „nur Rückrunde“) oder eine Zahl Q, die die
    /// Termin-Entscheidungen des Spiels auf ihre Stelle festlegen. <c>null</c>, wenn es in der Runde kein solches Spiel gibt.
    /// </summary>
    private (List<(int Spalte, double Faktor)> E, double K)? Stelle(int t, int j, int r, FesterTermin[] eigeneFeste, Spielzaehler z)
    {
        int festeStelle = Array.FindIndex(eigeneFeste, f => (f.Heim == j || f.Gast == j) && d.RundeZuDatum(f.Datum) == r);
        if (festeStelle >= 0)
        {
            return ([], festeStelle);
        }

        List<int> moeglich = jeMannschaft[t]
            .Where(i => d.WunschRunde[termine[i].Wunsch] == r && (d.SpielHeim[termine[i].Spiel] == j || d.SpielGast[termine[i].Spiel] == j))
            .ToList();
        if (moeglich.Count == 0)
        {
            return null;
        }

        int g = z.Spiele;
        int q = modell.Zahl(0, g);
        foreach (int i in moeglich)
        {
            // Q ≥ Stelle − g · (1 − x) und Q ≤ Stelle + g · (1 − x).
            (List<(int Spalte, double Faktor)> stelle, double k) = z.Stelle(t, i);
            int x = termine[i].Spalte;
            modell.Zeile(Zusammenfassen([(q, 1.0), .. stelle.Select(e => (e.Spalte, -e.Faktor)), (x, -g)]), k - g, double.PositiveInfinity);
            modell.Zeile(Zusammenfassen([(q, 1.0), .. stelle.Select(e => (e.Spalte, -e.Faktor)), (x, g)]), double.NegativeInfinity, k + g);
        }

        return ([(q, 1.0)], 0);
    }
}
