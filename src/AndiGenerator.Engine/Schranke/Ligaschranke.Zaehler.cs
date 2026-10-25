// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Zähler der Spiele je Mannschaft und Tag (nur für die Teilneuplanung): Grundlage für Kriterien, die nach der Reihenfolge der
/// eigenen Spiele zählen (Spieltage, Abstand Heim/Auswärts, Setzliste).
/// </summary>
public sealed partial class Ligaschranke
{
    private Spielzaehler? spielzaehler;
    private bool spielzaehlerAngelegt;

    /// <summary>
    /// Zählt Verstöße eines Plans mit dem Modell der Teilneuplanung (für Tests: Wo es genau ist, muss es dieselben Zahlen liefern
    /// wie die Bewertung).
    /// </summary>
    /// <param name="staffel">Die Staffel (bei „nur Rückrunde“ mit der Vorrunde als bestehendem Plan).</param>
    /// <param name="plan">Der Plan.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="loeser">Der Löser (HiGHS).</param>
    /// <param name="kriterien">Die Kriterien.</param>
    /// <returns>Je Kriterium die Verstöße im Modell (fehlt, wenn das Modell es nicht kennt); <c>null</c>, wenn der Plan nicht passt.</returns>
    internal static Dictionary<Kostenkriterium, int>? ImModellZaehlen(Staffel staffel, IReadOnlyList<Spiel> plan, Berechnungsoptionen optionen, ISchrankenloeser loeser, IEnumerable<Kostenkriterium> kriterien)
    {
        RefPlan referenz = RefPlan.Laden(optionen.Rundenplanung == Rundenplanung.NurRueckrunde ? staffel : staffel with { BestehenderSpielplan = [] }, optionen);
        var d = new KernDefinition(referenz);
        if (Grund(d) is not null)
        {
            return null;
        }

        var schranke = new Ligaschranke(d);
        Dictionary<Kostenkriterium, List<(int Spalte, double Faktor)>> ziele = kriterien
            .Select(k => (k, Ziel: schranke.Ziel(k)))
            .Where(e => e.Ziel is not null)
            .ToDictionary(e => e.k, e => e.Ziel!);
        double[] x = schranke.Abbilden(plan, out _);
        foreach (int spalte in schranke.termine.Select(t => t.Spalte).Concat(schranke.ohneTermin.Select(u => u.Spalte)))
        {
            schranke.modell.Zeile([spalte], x[spalte], x[spalte]);
        }

        (_, _, double[]? werte, _) = Minimieren(schranke.modell, Zusammenfassen(ziele.Values.SelectMany(z => z)), loeser, TimeSpan.FromSeconds(60));
        return werte is null ? null : ziele.ToDictionary(e => e.Key, e => (int)Math.Round(Wert(e.Value, werte)));
    }

    /// <summary>Die Zähler; beim ersten Aufruf angelegt (<c>null</c>, wenn das Modell sie nicht abbilden kann).</summary>
    private Spielzaehler? Zaehler()
    {
        if (!spielzaehlerAngelegt)
        {
            spielzaehlerAngelegt = true;
            spielzaehler = Spielzaehler.Anlegen(this);
        }

        return spielzaehler;
    }

    /// <summary>
    /// Je Mannschaft t eine Zahl C(t, a): ihre Spiele bis einschließlich Tag a – nur an Tagen, an denen sie spielen kann (dazwischen
    /// gilt der vorige Stand, vor dem ersten die festen Spiele). Daraus die Stelle eines Spiels in der Liste der eigenen Spiele
    /// (wie <c>KernPlan.Spiele</c>, nach Datum und Uhrzeit sortiert). Nur, wenn jede Mannschaft gleich viele Spiele hat und feste
    /// Spiele (Vorrunde bei „nur Rückrunde“) alle vor der geplanten Runde liegen; Spiele ohne Termin zählen nicht mit.
    /// </summary>
    private sealed class Spielzaehler
    {
        private readonly Ligaschranke s;

        /// <summary>Je Mannschaft die Tage, an denen sie spielen kann, mit der Spalte C(t, Tag) (aufsteigend).</summary>
        private readonly List<(int Tag, int Spalte)>[] zaehler;

        /// <summary>Je Mannschaft und Tag ihre Termin-Entscheidungen (Index in <see cref="termine"/>).</summary>
        private readonly Dictionary<int, List<int>>[] jeTag;

        /// <summary>Je Mannschaft und Spiel (bzw. Paarung) der früheste und der späteste mögliche Tag, je aufsteigend.</summary>
        private readonly (int[] Frueheste, int[] Spaeteste)[] grenzen;

        private Spielzaehler(Ligaschranke s, int fest, int spiele)
        {
            this.s = s;
            Fest = fest;
            Spiele = spiele;
            KernDefinition d = s.d;
            zaehler = new List<(int Tag, int Spalte)>[d.N];
            jeTag = new Dictionary<int, List<int>>[d.N];
            grenzen = new (int[], int[])[d.N];
            for (int t = 0; t < d.N; t++)
            {
                zaehler[t] = [];
                jeTag[t] = [];
                int vorher = -1;
                foreach (IGrouping<int, int> tag in s.jeMannschaft[t].GroupBy(i => d.WunschTag[s.termine[i].Wunsch]).OrderBy(gr => gr.Key))
                {
                    // C(t, Tag) = C(t, voriger Tag) + Spiele an diesem Tag (am ersten: + feste Spiele).
                    int c = s.modell.Zahl(0, spiele);
                    var eintraege = new List<(int Spalte, double Faktor)> { (c, 1.0) };
                    if (vorher >= 0)
                    {
                        eintraege.Add((vorher, -1.0));
                    }

                    eintraege.AddRange(tag.Select(i => (s.termine[i].Spalte, -1.0)));
                    double rest = vorher >= 0 ? 0 : fest;
                    s.modell.Zeile(eintraege, rest, rest);
                    zaehler[t].Add((tag.Key, c));
                    jeTag[t][tag.Key] = [.. tag];
                    vorher = c;
                }

                var jeGruppe = s.jeMannschaft[t]
                    .GroupBy(i => s.Gruppe(s.termine[i].Spiel))
                    .Select(gr => (Frueh: gr.Min(i => d.WunschTag[s.termine[i].Wunsch]), Spaet: gr.Max(i => d.WunschTag[s.termine[i].Wunsch])))
                    .ToList();
                grenzen[t] = (jeGruppe.Select(e => e.Frueh).Order().ToArray(), jeGruppe.Select(e => e.Spaet).Order().ToArray());
            }
        }

        /// <summary>Holt die Zahl der festen Spiele je Mannschaft (vor der geplanten Runde).</summary>
        public int Fest { get; }

        /// <summary>Holt die Zahl der Spiele je Mannschaft (mit den festen).</summary>
        public int Spiele { get; }

        /// <summary>Legt die Zähler an, wenn das Modell sie abbilden kann.</summary>
        public static Spielzaehler? Anlegen(Ligaschranke s)
        {
            KernDefinition d = s.d;
            if (s.termine.Count == 0)
            {
                return null;
            }

            // Spiele je Mannschaft: die festen und je „ohne Termin“-Spalte eines (sie steht für ein Spiel bzw. eine Paarung).
            int[] fest = new int[d.N];
            foreach (FesterTermin f in s.feste)
            {
                fest[f.Heim]++;
                fest[f.Gast]++;
            }

            int[] spiele = [.. fest];
            foreach ((_, int spiel) in s.ohneTermin)
            {
                spiele[d.SpielHeim[spiel]]++;
                spiele[d.SpielGast[spiel]]++;
            }

            int ersterTag = s.termine.Min(t => d.WunschTag[t.Wunsch]);
            if (spiele.Distinct().Count() != 1 || fest.Distinct().Count() != 1 || Array.Exists(s.feste, f => DelphiDatum.Trunc(f.Datum) >= ersterTag)
                || spiele[0] - fest[0] < 1)
            {
                return null;
            }

            return new Spielzaehler(s, fest[0], spiele[0]);
        }

        /// <summary>Die Spalte C(t, a), wenn t am Tag a spielen kann; sonst −1.</summary>
        public int Am(int t, int a)
        {
            int c = zaehler[t].FindIndex(e => e.Tag == a);
            return c >= 0 ? zaehler[t][c].Spalte : -1;
        }

        /// <summary>Die Spalte, die C(t, x) angibt (der letzte Tag bis x, an dem t spielen kann); −1, wenn es nur die festen sind.</summary>
        public int Bis(int t, int x)
        {
            int c = zaehler[t].FindLastIndex(e => e.Tag <= x);
            return c >= 0 ? zaehler[t][c].Spalte : -1;
        }

        /// <summary>C(t, x) als Ausdruck: Spalte bzw. die festen Spiele als Konstante.</summary>
        public (List<(int Spalte, double Faktor)> Eintraege, double Konstante) BisAusdruck(int t, int x) =>
            Bis(t, x) is var c and >= 0 ? ([(c, 1.0)], 0) : ([], Fest);

        /// <summary>
        /// Die Stelle (ab 0) des Spiels der Termin-Entscheidung <paramref name="i"/> in der Liste der eigenen Spiele von t, wenn es
        /// stattfindet: die Spiele bis zum Vortag und die am selben Tag früher.
        /// </summary>
        public (List<(int Spalte, double Faktor)> Eintraege, double Konstante) Stelle(int t, int i)
        {
            KernDefinition d = s.d;
            int w = s.termine[i].Wunsch;
            int tag = d.WunschTag[w];
            (List<(int Spalte, double Faktor)> eintraege, double konstante) = BisAusdruck(t, tag - 1);
            eintraege.AddRange(jeTag[t][tag].Where(j => j != i && d.WunschDatum[s.termine[j].Wunsch] < d.WunschDatum[w]).Select(j => (s.termine[j].Spalte, 1.0)));
            return (eintraege, konstante);
        }

        /// <summary>So viele Spiele hat t bis zum Tag x sicher (feste und die, die spätestens dann liegen müssen).</summary>
        public int Unten(int t, int x) => Fest + Anzahl(grenzen[t].Spaeteste, x);

        /// <summary>So viele Spiele kann t bis zum Tag x höchstens haben.</summary>
        public int Oben(int t, int x) => Fest + Anzahl(grenzen[t].Frueheste, x);

        private static int Anzahl(int[] tage, int x) => tage.Count(tag => tag <= x);
    }
}
