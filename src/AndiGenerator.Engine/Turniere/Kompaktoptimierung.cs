// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Konstruiert Pläne aus Kompaktspieltagen gezielt nach den Kriterien statt zufällig (nicht im Original; Peter,
/// 13.10.2026): Jeder mögliche Spieltag an jedem möglichen Tag ist eine Ja/Nein-Entscheidung, jede Paarung kommt genau
/// einmal vor, jede Mannschaft spielt höchstens einmal am Tag und richtet höchstens zwei Spieltage aus. Gesucht ist der
/// Plan mit den wenigsten Punkten (wie in der Bewertung der Oberfläche): 10 Punkte je Verstoß gegen die Meldungen, 15 je
/// Mannschaft ohne 3er- oder 4er-Heimspieltag (Peter, 13.10.2026: erhöht), 2 je Mannschaft ganz ohne Heimspiel, 1 Punkt je 10 % mehr Kilometer als der Bezug
/// (Einzelfahrten doppelt), 3 je 3er-Spieltag, wenn ein Plan nur aus 4er-Spieltagen geht, 3 je fehlendem Spitzen- bzw.
/// Abstiegsspieltag (die vier Stärksten bzw. Schwächsten als 4er-Spieltag oder die drei als 3er-Spieltag), bis zu einem
/// halben Punkt, wenn er früh liegt. Der Löser (<see cref="IGanzzahlloeser"/>, HiGHS) findet beweisbar den besten Plan, wenn die
/// Zeit reicht.
/// </summary>
internal sealed class Kompaktoptimierung
{
    private readonly Kompaktmodell modell;
    private readonly IGanzzahlloeser loeser;
    private readonly Kompaktkandidat[] alle;
    private readonly string[] block;
    private readonly bool[][] gruppenTag;

    /// <summary>Bereitet die Optimierung vor.</summary>
    /// <param name="modell">Die aufbereitete Aufgabe.</param>
    /// <param name="loeser">Der Löser.</param>
    public Kompaktoptimierung(Kompaktmodell modell, IGanzzahlloeser loeser)
    {
        this.modell = modell;
        this.loeser = loeser;
        var gesehen = new HashSet<Kompaktkandidat>(ReferenceEqualityComparer.Instance);
        alle = modell.JePaar.SelectMany(p => p).Where(gesehen.Add).ToArray();
        block = alle.Select(Block).ToArray();
        gruppenTag = modell.Gruppen.Select(g => alle.Select(k => IstGruppentag(k, g)).ToArray()).ToArray();
        KmSchaetzung = Schaetzen();
    }

    /// <summary>Holt eine untere Schätzung der Kilometer (Maßstab, solange kein kürzester Plan bekannt ist).</summary>
    public double KmSchaetzung { get; }

    /// <summary>Ob ein Spieltag Spitzen- bzw. Abstiegsspieltag der Gruppe ist: 4er-Spieltag der vier (die ersten beiden gegeneinander) oder 3er-Spieltag der ersten drei.</summary>
    /// <param name="k">Der Spieltag.</param>
    /// <param name="g">Die Gruppe, die stärkste bzw. schwächste Mannschaft zuerst.</param>
    /// <returns><c>true</c>, wenn er es ist.</returns>
    public static bool IstGruppentag(Kompaktkandidat k, int[] g)
    {
        if (k.Form == Turnierform.Dreier)
        {
            return g.Length >= 3 && g[..3].All(k.Mannschaften.Contains);
        }

        if (k.Form != Turnierform.Vierer || g.Length != 4 || !g.All(k.Mannschaften.Contains))
        {
            return false;
        }

        bool ErsteSeite(int t) => t == k.Mannschaften[0] || t == k.Mannschaften[1];
        return ErsteSeite(g[0]) != ErsteSeite(g[1]);
    }

    /// <summary>
    /// Sucht den besten Plan für einen Auftrag. Mehrere Läufe des Lösers mit verschiedenen Startwerten rechnen parallel
    /// (der Löser selbst rechnet in einem Thread); sobald einer den besten Plan bewiesen hat, hören die anderen auf, sonst
    /// gilt der beste Plan aller Läufe.
    /// </summary>
    /// <param name="auftrag">Was gesucht wird.</param>
    /// <param name="start">Ein bekannter Plan als Startpunkt, oder <c>null</c>.</param>
    /// <param name="parallel">Zahl paralleler Läufe.</param>
    /// <param name="aufhoeren">Wird während der Suche gefragt (darf für die Pause blockieren).</param>
    /// <param name="zeitgrenze">Harte Zeitgrenze für den Löser (auch ohne Rückfrage), oder <c>null</c>.</param>
    /// <returns>Der Plan, oder <c>null</c>, wenn keiner gefunden wurde; und ob er beweisbar der beste ist.</returns>
    public (Kompaktloesung? Loesung, bool Optimal) Suchen(Kompaktauftrag auftrag, Kompaktloesung? start, int parallel, Func<bool> aufhoeren, TimeSpan? zeitgrenze = null)
    {
        ArgumentNullException.ThrowIfNull(auftrag);
        ArgumentNullException.ThrowIfNull(aufhoeren);
        var uhr = Stopwatch.StartNew();
        TimeSpan? Rest() => zeitgrenze is TimeSpan g ? TimeSpan.FromTicks(Math.Max(TimeSpan.TicksPerSecond, (g - uhr.Elapsed).Ticks)) : null;

        // Erst nur Spieltage ohne Verstoß (kleineres Modell, meist reicht das), sonst auch mit Verstößen (10 Punkte je Verstoß).
        (Kompaktloesung? loesung, bool optimal) = SuchenBis(auftrag, 0, start, parallel, aufhoeren, Rest());
        if (loesung is null && !aufhoeren())
        {
            (loesung, optimal) = SuchenBis(auftrag, Kompaktmodell.MaxVerstoesseJeSpieltag, start, parallel, aufhoeren, Rest());
        }

        return (loesung, optimal);
    }

    private static string Block(Kompaktkandidat k) => string.Create(CultureInfo.InvariantCulture, $"{(int)k.Form}:{string.Join(',', k.Mannschaften)}");

    /// <summary>Sucht nur mit Spieltagen bis zu so vielen Verstößen, in mehreren Läufen parallel.</summary>
    private (Kompaktloesung? Loesung, bool Optimal) SuchenBis(Kompaktauftrag auftrag, int verstoesse, Kompaktloesung? start, int parallel, Func<bool> aufhoeren, TimeSpan? zeitgrenze)
    {
        (Ganzzahlmodell m, int[] spalteZuKandidat) = Aufbauen(auftrag, verstoesse);
        double[]? startwerte = start is null ? null : Startwerte(m, spalteZuKandidat, start, auftrag);
        int bewiesen = 0;
        bool Aufhoeren() => Volatile.Read(ref bewiesen) != 0 || aufhoeren();
        var laeufe = new (double[]? Werte, bool Optimal)[Math.Max(1, parallel)];
        Parallel.For(0, laeufe.Length, new ParallelOptions { MaxDegreeOfParallelism = laeufe.Length }, i =>
        {
            laeufe[i] = loeser.Loesen(m, startwerte, i + 1, Aufhoeren, zeitgrenze);
            if (laeufe[i].Optimal)
            {
                Interlocked.Exchange(ref bewiesen, 1);
            }
        });

        Kompaktloesung? beste = null;
        double besteKosten = double.PositiveInfinity;
        bool optimal = false;
        foreach ((double[]? werte, bool bewiesenerLauf) in laeufe.Append<(double[]? Werte, bool Optimal)>((startwerte, false)).Where(l => l.Werte is not null))
        {
            List<Kompaktkandidat> wahl = [];
            double kosten = 0;
            for (int s = 0; s < werte!.Length; s++)
            {
                if (werte[s] > 0.5)
                {
                    kosten += m.Kosten[s];
                    if (s < spalteZuKandidat.Length)
                    {
                        wahl.Add(alle[spalteZuKandidat[s]]);
                    }
                }
            }

            if (!Gueltig(wahl))
            {
                continue;
            }

            if (kosten < besteKosten - 1e-9)
            {
                beste = new Kompaktloesung(modell, wahl);
                besteKosten = kosten;
                optimal = bewiesenerLauf;
            }
            else if (bewiesenerLauf && kosten <= besteKosten + 1e-9)
            {
                optimal = true;
            }
        }

        return (beste, optimal);
    }

    /// <summary>Prüft selbst, ob ein Plan vollständig ist: jede Paarung genau einmal, niemand zweimal am Tag.</summary>
    private bool Gueltig(List<Kompaktkandidat> wahl)
    {
        var gedeckt = new int[modell.Paare];
        var belegt = new HashSet<(int Mannschaft, int Tag)>();
        foreach (Kompaktkandidat k in wahl)
        {
            foreach (int p in k.Paare)
            {
                gedeckt[p]++;
            }

            if (!Array.TrueForAll(k.Mannschaften, t => belegt.Add((t, k.Tag))))
            {
                return false;
            }
        }

        return Array.TrueForAll(gedeckt, x => x == 1);
    }

    private (Ganzzahlmodell Modell, int[] SpalteZuKandidat) Aufbauen(Kompaktauftrag auftrag, int verstoesse)
    {
        var m = new Ganzzahlmodell();
        int n = modell.N;
        double bezug = auftrag.KmBezug ?? KmSchaetzung;
        double letzter = Math.Max(1, modell.T - 1);
        var je = new List<int>();
        for (int i = 0; i < alle.Length; i++)
        {
            Kompaktkandidat k = alle[i];
            if ((auftrag.NurVierer && k.Form == Turnierform.Dreier) || k.Verstoesse > verstoesse)
            {
                continue;
            }

            double punkte = (10.0 * k.Km * (k.IstEinzel ? 2 : 1) / bezug) + (10.0 * k.Verstoesse);
            if (auftrag.DreierStrafe && k.Form == Turnierform.Dreier)
            {
                punkte += 3;
            }

            if (auftrag.MitGruppen)
            {
                for (int g = 0; g < gruppenTag.Length; g++)
                {
                    punkte += gruppenTag[g][i] ? 0.5 * (letzter - k.Tag) / letzter : 0;
                }
            }

            m.Spalte(punkte);
            je.Add(i);
        }

        int[] spalten = [.. je];
        int ohneHeimspieltag = m.Spalten;
        for (int t = 0; t < n; t++)
        {
            m.Spalte(15);
        }

        int ohneHeimspiel = m.Spalten;
        for (int t = 0; t < n; t++)
        {
            m.Spalte(2);
        }

        int gruppeFehlt = m.Spalten;
        if (auftrag.MitGruppen)
        {
            for (int g = 0; g < gruppenTag.Length; g++)
            {
                m.Spalte(3, !auftrag.GruppenPflicht);
            }
        }

        IEnumerable<int> Spalten(Func<Kompaktkandidat, bool> wo) => Enumerable.Range(0, spalten.Length).Where(s => wo(alle[spalten[s]]));

        // Jede Paarung genau einmal.
        for (int p = 0; p < modell.Paare; p++)
        {
            int paar = p;
            m.Zeile(Spalten(k => Array.IndexOf(k.Paare, paar) >= 0), 1, 1);
        }

        // Jede Mannschaft höchstens einmal am Tag.
        var amTag = new Dictionary<(int, int), List<int>>();
        for (int s = 0; s < spalten.Length; s++)
        {
            Kompaktkandidat k = alle[spalten[s]];
            foreach (int t in k.Mannschaften)
            {
                if (!amTag.TryGetValue((t, k.Tag), out List<int>? liste))
                {
                    liste = [];
                    amTag[(t, k.Tag)] = liste;
                }

                liste.Add(s);
            }
        }

        foreach (List<int> liste in amTag.Values.Where(l => l.Count > 1))
        {
            m.Zeile(liste, 0, 1);
        }

        for (int t = 0; t < n; t++)
        {
            int mannschaft = t;

            // Höchstens zwei Spieltage ausrichten; sonst zählt sie als „ohne Heimspieltag“ bzw. „ganz ohne Heimspiel“.
            m.Zeile(Spalten(k => !k.IstEinzel && k.Ausrichter == mannschaft), 0, 2);
            m.Zeile([.. Spalten(k => !k.IstEinzel && k.Ausrichter == mannschaft), ohneHeimspieltag + t], 1, double.PositiveInfinity);
            m.Zeile([.. Spalten(k => k.Ausrichter == mannschaft), ohneHeimspiel + t], 1, double.PositiveInfinity);

            // Höchstens eine Einzelbegegnung je Mannschaft.
            m.Zeile(Spalten(k => k.IstEinzel && k.Mannschaften.Contains(mannschaft)), 0, 1);
        }

        if (auftrag.MitGruppen)
        {
            for (int g = 0; g < gruppenTag.Length; g++)
            {
                bool[] tag = gruppenTag[g];
                m.Zeile([.. Enumerable.Range(0, spalten.Length).Where(s => tag[spalten[s]]), gruppeFehlt + g], 1, double.PositiveInfinity);
            }
        }

        // Andere Pläne als die schon gefundenen: nicht dieselben Spieltage (an welchen Tagen auch immer).
        foreach (Kompaktloesung frueher in auftrag.Ausser)
        {
            var bloecke = frueher.Turniere.Select(Block).ToHashSet(StringComparer.Ordinal);
            m.Zeile(Enumerable.Range(0, spalten.Length).Where(s => bloecke.Contains(block[spalten[s]])), 0, bloecke.Count - 1);
        }

        return (m, spalten);
    }

    /// <summary>
    /// Die Spaltenwerte eines bekannten Plans: seine Spieltage, „ohne Heimspieltag“, „ganz ohne Heimspiel“ und „Gruppe
    /// fehlt“ passend; <c>null</c>, wenn ein Spieltag im Modell nicht vorkommt oder der Plan ausgeschlossen ist.
    /// </summary>
    private double[]? Startwerte(Ganzzahlmodell m, int[] spalteZuKandidat, Kompaktloesung start, Kompaktauftrag auftrag)
    {
        var spalteVon = new Dictionary<(string, int), int>();
        for (int s = 0; s < spalteZuKandidat.Length; s++)
        {
            spalteVon[(block[spalteZuKandidat[s]], alle[spalteZuKandidat[s]].Tag)] = s;
        }

        // Ein Startplan mit denselben Spieltagen wie ein ausgeschlossener Plan passt nicht.
        var startbloecke = start.Turniere.Select(Block).ToHashSet(StringComparer.Ordinal);
        if (auftrag.Ausser.Any(f => f.Turniere.All(k => startbloecke.Contains(Block(k)))))
        {
            return null;
        }

        var werte = new double[m.Spalten];
        int n = modell.N;
        var gruppentag = new bool[gruppenTag.Length];
        foreach (Kompaktkandidat k in start.Turniere)
        {
            if (!spalteVon.TryGetValue((Block(k), k.Tag), out int s))
            {
                return null;
            }

            werte[s] = 1;
            for (int g = 0; g < gruppentag.Length; g++)
            {
                gruppentag[g] |= gruppenTag[g][spalteZuKandidat[s]];
            }
        }

        int basis = spalteZuKandidat.Length;
        for (int t = 0; t < n; t++)
        {
            int mannschaft = t;
            werte[basis + t] = Array.Exists(start.Turniere, k => !k.IstEinzel && k.Ausrichter == mannschaft) ? 0 : 1;
            werte[basis + n + t] = Array.Exists(start.Turniere, k => k.Ausrichter == mannschaft) ? 0 : 1;
        }

        if (auftrag.MitGruppen)
        {
            for (int g = 0; g < gruppentag.Length; g++)
            {
                werte[basis + (2 * n) + g] = gruppentag[g] ? 0 : 1;
            }
        }

        return werte;
    }

    /// <summary>Je Paarung der günstigste Anteil eines Spieltags (Kilometer geteilt durch seine Paarungen), aufsummiert.</summary>
    private double Schaetzen()
    {
        double summe = 0;
        foreach (Kompaktkandidat[] je in modell.JePaar)
        {
            summe += je.Select(k => k.Km * (k.IstEinzel ? 2 : 1) / k.Paare.Length).DefaultIfEmpty(0).Min();
        }

        return Math.Max(1, summe);
    }
}
