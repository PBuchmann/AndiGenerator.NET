// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Der einfache Modus (Peter, 28.10.2026): der schnellste Weg zu einem Plan, der die Pflicht erfüllt und die Wünsche der
/// Vereine bestmöglich – danach sucht er an der <b>Grenze des Machbaren</b> (<see cref="Paretofront"/>): Pläne, in denen sich
/// keine Gruppe mehr senken lässt, ohne dass eine andere steigt. Der Klassenleiter steuert von außen
/// (<see cref="Schnellsteuerung"/>), welche Gruppe ihm wichtiger ist; „der beste Plan“ ist der beste für seine Wahl.
/// <list type="number">
/// <item><b>Phase 1 – Pflicht und Wünsche:</b> Die Inseln suchen sofort; gleichzeitig rechnet HiGHS die Kette A1 → Pflicht →
/// Wünsche (die B-Varianten intern als Startpläne), ab dem besten Plan der Inseln.</item>
/// <item><b>Phase 2 – Glätten:</b> Die Grundeinteilung (B nur die Wünsche) glättet mit allen Kernen; ihr Plan ist der Bezug der
/// Gruppen. Danach sind Pflicht und Wünsche je Kriterium festgelegt – sie werden nie geopfert.</item>
/// <item><b>Phase 3 – Rennen an der Grenze:</b> Kandidaten sind die Grundeinteilung und je Gruppe ein Fokus (ihre Kriterien
/// zusätzlich in B). Je Kandidat rechnen Inseln und Teilneuplanung wie im Automodus; die C-Reihenfolge folgt den Gewichten.
/// Nach jeder Runde behält die Grenze alle nicht dominierten Pläne, und die bessere Hälfte der Kandidaten nach dem Maßstab
/// bekommt die doppelte Zeit (Successive Halving). Wählt der Klassenleiter einen Fokus, rechnet dessen Kandidat wieder mit und
/// scheidet nicht aus.</item>
/// </list>
/// </summary>
public static class Schnellplaner
{
    /// <summary>Plant, bis <paramref name="abbruch"/> ausgelöst wird.</summary>
    /// <param name="staffel">Die Staffel (bei „nur Rückrunde“ mit der Vorrunde als bestehendem Plan).</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="loeser">Je Lauf ein Löser (HiGHS), Argument ist der Startwert; oder <c>null</c> ohne HiGHS.</param>
    /// <param name="einstellungen">Zeiten und Kerne.</param>
    /// <param name="steuerung">Die Steuerung von außen (Fokus, Gewichte), oder <c>null</c> (Standard).</param>
    /// <param name="melden">Meldet jeden Schritt, oder <c>null</c>.</param>
    /// <param name="abbruch">Beendet die Planung.</param>
    /// <returns>Das Ergebnis.</returns>
    public static Schnellergebnis Planen(Staffel staffel, Berechnungsoptionen optionen, Func<int, ISchrankenloeser>? loeser, Schnellplanereinstellungen einstellungen, Schnellsteuerung? steuerung = null, Action<Schnellstand>? melden = null, CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(einstellungen);
        return new Lauf(staffel, optionen, loeser, einstellungen, steuerung ?? new Schnellsteuerung(), melden ?? (_ => { }), abbruch).Ausfuehren();
    }

    /// <summary>Die Grundeinteilung: A die Pflicht, B die Wünsche der Vereine, C der Rest wie im Standard.</summary>
    /// <returns>Die Einteilung.</returns>
    public static Stufeneinteilung Grundeinteilung() =>
        new Stufeneinteilung(Planguete.Pflicht, Planguete.Wunschkriterien, []).Vervollstaendigt();

    /// <summary>Der Fokus auf eine Gruppe: ihre Kriterien (mit Anzahl) stehen zusätzlich in B.</summary>
    /// <param name="gruppe">Index in <see cref="Massstab.Gruppen"/>.</param>
    /// <param name="basis">Die Einteilung, von der er ausgeht (Standard: die Grundeinteilung).</param>
    /// <returns>Die Einteilung; <c>null</c>, wenn kein Kriterium der Gruppe in B stehen kann (Spieltage).</returns>
    public static Stufeneinteilung? Fokuseinteilung(int gruppe, Stufeneinteilung? basis = null)
    {
        Kostenkriterium[] kriterien = [.. Massstab.Gruppen[gruppe].Kriterien.Where(Stufeneinteilung.HatAnzahl)];
        return kriterien.Length == 0 ? null : kriterien.Aggregate(basis ?? Grundeinteilung(), (e, k) => e.Einstufen(k, Stufe.B));
    }

    /// <summary>Ordnet die Stufe C nach den Gewichten (schwerere Gruppen zuerst, sonst wie bisher).</summary>
    /// <param name="einteilung">Die Einteilung.</param>
    /// <param name="massstab">Der Maßstab.</param>
    /// <returns>Die Einteilung mit geordnetem C.</returns>
    public static Stufeneinteilung NachGewichten(Stufeneinteilung einteilung, Massstab massstab)
    {
        ArgumentNullException.ThrowIfNull(einteilung);
        ArgumentNullException.ThrowIfNull(massstab);
        Stufeneinteilung voll = einteilung.Vervollstaendigt();
        double Gewicht(Kostenkriterium k) => Massstab.GruppeVon(k) is int g and >= 0 ? massstab.Gewichte[g] * massstab.Faktor(k) : 0;
        return voll with { C = [.. voll.C.Select((k, i) => (k, i)).OrderByDescending(e => Gewicht(e.k)).ThenBy(e => e.i).Select(e => e.k)] };
    }

    /// <summary>Ein Lauf des einfachen Modus mit seinem Zustand.</summary>
    private sealed class Lauf(Staffel staffel, Berechnungsoptionen optionen, Func<int, ISchrankenloeser>? loeser, Schnellplanereinstellungen e, Schnellsteuerung steuerung, Action<Schnellstand> melden, CancellationToken abbruch)
    {
        private readonly Stopwatch uhr = Stopwatch.StartNew();

        /// <summary>Die Einteilung zum Glätten (Phase 2 und 3): die Grundeinteilung.</summary>
        private readonly Stufeneinteilung glaetten = Grundeinteilung();
        private readonly Lock sperre = new();
        private readonly List<string> protokoll = [];
        private readonly List<Schnellkandidat> kandidaten = [];
        private Paretofront grenze = new();
        private Schrankenbericht? bericht;
        private Dictionary<Kostenkriterium, int>? naehe;
        private Grenzplan? gewaehlt;
        private (IReadOnlyList<Spiel> Plan, Planguete Guete)? vorlaeufig;
        private double? ersterGueltiger;
        private double? aUndB;
        private int phase = 1;
        private int startwert = e.Startwert ?? Random.Shared.Next(1, 1_000_000);
        private int? schrankeA;

        public Schnellergebnis Ausfuehren()
        {
            Stufeneinteilung grund = Grundeinteilung();
            bericht = PhaseEins(grund);
            (IReadOnlyList<Spiel> Plan, Planguete Guete)? grundplan = vorlaeufig;
            if (grundplan is not { } g || abbruch.IsCancellationRequested)
            {
                return Ergebnis(grundplan, bericht?.Proben ?? []);
            }

            phase = 2;
            schrankeA = bericht?.SchrankeA;
            var grenzen = new Dictionary<Kostenkriterium, int>();
            foreach (Kostenkriterium k in Planguete.Pflicht.Concat(Planguete.Wunschkriterien))
            {
                grenzen[k] = g.Guete.Anzahl[(int)k];
            }

            // Erst mit allen Kernen die Grundeinteilung glätten: Die Kandidaten zweigen von einem geglätteten Plan ab, und ihr
            // Unterschied zeigt sich in einer kurzen Runde (vom ungeglätteten Plan aus gewann sonst, wer am schnellsten glättet).
            var basis = new Schnellkandidat("Grundeinteilung", null, glaetten, g.Plan, g.Guete);
            if (e.Vorglaetten > TimeSpan.Zero && !abbruch.IsCancellationRequested)
            {
                Notieren($"Phase 2: Grundplan glätten ({e.Vorglaetten.TotalSeconds:0} s, alle Kerne)");
                Rechnen(basis, grenzen, e.Vorglaetten, Math.Max(1, e.Kerne - (loeser is null ? 0 : 1)));
            }

            // Bezug der Gruppen ist der geglättete Grundplan; Pflicht und Wünsche bleiben höchstens so.
            (IReadOnlyList<Spiel> Plan, Planguete Guete) geglaettet = basis.Bester(Massstab.Standard);
            steuerung.BezugSetzen(geglaettet.Guete.Anzahl);
            foreach (Kostenkriterium k in grenzen.Keys)
            {
                grenzen[k] = geglaettet.Guete.Anzahl[(int)k];
            }

            Notieren($"Grundplan (Bezug der Gruppen): {Beschreiben(geglaettet.Guete)}");
            Aufnehmen(geglaettet.Plan, geglaettet.Guete, "Grundplan");
            Kandidatenbilden(basis, geglaettet);
            phase = 3;
            Rennen(grenzen);
            return Ergebnis(grundplan, bericht?.Proben ?? []);
        }

        private static List<Schnellkandidat> Rangfolge(IEnumerable<Schnellkandidat> liste, Massstab massstab)
        {
            List<Schnellkandidat> sortiert = [.. liste];
            sortiert.Sort((x, y) =>
            {
                Planguete gx = x.Bester(massstab).Guete;
                Planguete gy = y.Bester(massstab).Guete;
                if (massstab.IstBesser(gx, gy))
                {
                    return -1;
                }

                return massstab.IstBesser(gy, gx) ? 1 : 0;
            });
            return sortiert;
        }

        /// <summary>Ein Kandidat mit Fokus auf eine Gruppe (ihre Kriterien in B, sonst – Spieltage – vorn in C und schwerer).</summary>
        private static Schnellkandidat Fokuskandidat(int g, IReadOnlyList<Spiel> plan, Planguete guete, bool strikt, Stufeneinteilung basis) => strikt
            ? new("Hebel " + Massstab.Gruppen[g].Name, g, Fokuseinteilung(g, basis) ?? basis, plan, guete, strikt: true)
            : new("Richtung " + Massstab.Gruppen[g].Name, g, basis, plan, guete);

        /// <summary>Die Richtung eines Kandidaten: der Maßstab mit seiner Gruppe im Fokus (die Grundeinteilung ausgewogen).</summary>
        private static Massstab Richtung(Schnellkandidat kandidat, Massstab massstab) => kandidat.Gruppe switch
        {
            int g when kandidat.Strikt => massstab with { Fokus = g },
            int g => (massstab with { Fokus = null }).MitGewicht(g, massstab.Gewichte[g] * 3),
            _ => massstab with { Fokus = null },
        };

        /// <summary>Phase 1: Inseln sofort, HiGHS die Kette A → B.</summary>
        private Schrankenbericht? PhaseEins(Stufeneinteilung grund)
        {
            Notieren("Phase 1: Pflicht und Wünsche der Vereine (Inseln und HiGHS)");
            Optimierungseinstellungen einst = Optimierungseinstellungen.Standard with
            {
                Kerne = e.Kerne,
                Automodus = true,
                AutoStufen = grund,
                Startwert = startwert,
            };
            using var insel = new Inseloptimierer(staffel, optionen, einst);
            insel.Starten();
            using var takt = new Timer(_ => Vorlaeufig(insel.BesterStand().Spiele, "Inseln"), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            Schrankenbericht? schranke = null;
            if (loeser is not null)
            {
                schranke = Ligaschranke.Schnell(
                    staffel,
                    optionen,
                    loeser(startwert),
                    grund,
                    e.ZeitJeProbe,
                    f =>
                    {
                        if (f.Plan is { Count: > 0 } plan)
                        {
                            Vorlaeufig(plan, "HiGHS");
                            insel.PlanEinspielen(plan, $"HiGHS ({f.Text})", inselnNachziehen: true);
                        }

                        if (f.Zwischenbericht is not null)
                        {
                            lock (sperre)
                            {
                                aUndB ??= uhr.Elapsed.TotalSeconds;
                            }

                            Notieren("HiGHS: Pflicht und Wünsche fertig");
                        }
                        else if (!f.NurPlan)
                        {
                            Notieren("HiGHS: " + f.Text);
                        }
                    },
                    aufhoeren: () => abbruch.IsCancellationRequested || (aUndB is null && uhr.Elapsed > e.PhaseEinsHoechstens),
                    startplan: () => insel.BesterStand().Spiele,
                    laufzeit: e.Laufzeit);
                foreach (string hinweis in schranke.Hinweise)
                {
                    Notieren("HiGHS-Hinweis: " + hinweis);
                }
            }
            else
            {
                // Ohne HiGHS: Die Inseln allein, bis die Zeit von Phase 1 um ist.
                abbruch.WaitHandle.WaitOne(e.PhaseEinsHoechstens);
            }

            takt.Dispose();
            insel.Stoppen();
            Vorlaeufig(insel.BesterStand().Spiele, "Inseln");
            if (schranke is { Plan.Count: > 0 })
            {
                Vorlaeufig(schranke.Plan, "HiGHS");
            }

            if (vorlaeufig is { } b)
            {
                Notieren($"Pflicht und Wünsche: {b.Guete}");
            }

            return schranke;
        }

        /// <summary>Die Kandidaten: die Grundeinteilung und je Gruppe mit Verstößen ein Fokus.</summary>
        private void Kandidatenbilden(Schnellkandidat basis, (IReadOnlyList<Spiel> Plan, Planguete Guete) start)
        {
            lock (sperre)
            {
                kandidaten.Add(basis);
            }

            Massstab massstab = steuerung.Massstab;
            for (int g = 0; g < Massstab.Gruppen.Count; g++)
            {
                if (massstab.Gruppe(start.Guete, g) > 0)
                {
                    lock (sperre)
                    {
                        kandidaten.Add(Fokuskandidat(g, start.Plan, start.Guete, strikt: false, glaetten));
                    }
                }
            }

            Notieren($"Phase 3: Rennen an der Grenze mit {kandidaten.Count} Kandidaten");
        }

        /// <summary>
        /// Phase 3: das Rennen an der Grenze. Jede Runde rechnen so viele Kandidaten gleichzeitig, wie die Kerne erlauben; jeder
        /// beginnt beim besten Plan der Grenze für seine Richtung (so nutzt jeder, was die anderen gefunden haben). Reichen die
        /// Kerne nicht für alle, rechnet in jeder Stufe erst jeder einmal, dann bekommen die mit den meisten Erfolgen je Sekunde die
        /// Zeit (jeder fünfte Platz zufällig, der Fokus des Klassenleiters immer) – keiner scheidet für immer aus. Wählt der Klassenleiter einen Plan, beginnt eine neue Stufe.
        /// </summary>
        private void Rennen(Dictionary<Kostenkriterium, int> grenzen)
        {
            // Je Lauf ein Kern für die Inseln und einer für die Teilneuplanung.
            int gleichzeitig = Math.Max(1, e.Kerne / (loeser is null ? 1 : 2));
            int runde = 0;
            int version = -1;
            var zufall = new Random(startwert);

            // Je Kandidat 30 s in seiner ersten Runde der Stufe, dann jeweils doppelt so lang (höchstens die längste Runde).
            TimeSpan Dauer(Schnellkandidat k) =>
                TimeSpan.FromSeconds(Math.Min(e.ErsteRunde.TotalSeconds * Math.Pow(2, k.RundenInStufe), e.LaengsteRunde.TotalSeconds));
            while (!abbruch.IsCancellationRequested)
            {
                runde++;
                if (steuerung.Version != version)
                {
                    version = steuerung.Version;
                    if (steuerung.WahlAbholen() is { } wahl)
                    {
                        StufeBeginnen(wahl.Plan, wahl.Hebel);
                    }

                    FokusAktivieren(steuerung.Massstab);
                }

                Massstab massstab = steuerung.Massstab;
                List<Schnellkandidat> aktiv = Auswaehlen(gleichzeitig, massstab, zufall);
                int kerneJe = Math.Max(1, (e.Kerne / aktiv.Count) - (loeser is null ? 0 : 1));
                Parallel.ForEach(aktiv, new ParallelOptions { MaxDegreeOfParallelism = gleichzeitig }, k => Rechnen(k, grenzen, Dauer(k), kerneJe));
                Notieren($"Runde {runde} (Stufe {steuerung.Stufe}), Grenze {grenze.Plaene.Count} Pläne: "
                    + string.Join(" | ", aktiv.Select(k => $"{k.Name}: {Beschreiben(k.Letzter.Guete)}")));
                lock (sperre)
                {
                    kandidaten.ForEach(k => k.Vergessen(0.7));
                }
            }
        }

        /// <summary>Die Kandidaten der nächsten Runde (alle, wenn die Kerne reichen).</summary>
        private List<Schnellkandidat> Auswaehlen(int gleichzeitig, Massstab massstab, Random zufall)
        {
            List<Schnellkandidat> alle;
            lock (sperre)
            {
                alle = [.. kandidaten];
            }

            var auswahl = new List<Schnellkandidat>();
            if (alle.Count <= gleichzeitig)
            {
                auswahl.AddRange(alle);
            }
            else
            {
                auswahl.AddRange(alle.Where(k => k.Strikt && k.Gruppe == massstab.Fokus));
                List<Schnellkandidat> rest = [.. alle.Except(auswahl)
                    .OrderBy(k => k.RundenInStufe > 0)
                    .ThenByDescending(k => (k.Erfolge + 0.5) / (k.Sekunden + e.ErsteRunde.TotalSeconds))];
                while (auswahl.Count < gleichzeitig && rest.Count > 0)
                {
                    // Wer in dieser Stufe noch nicht gerechnet hat, kommt zuerst dran (jede Richtung einmal); dann nach Erfolg.
                    int i = rest[0].RundenInStufe > 0 && zufall.NextDouble() < 0.2 ? zufall.Next(rest.Count) : 0;
                    auswahl.Add(rest[i]);
                    rest.RemoveAt(i);
                }
            }

            foreach (Schnellkandidat k in alle)
            {
                k.Aktiv = auswahl.Contains(k);
            }

            return auswahl;
        }

        /// <summary>
        /// Eine neue Stufe: Es geht beim gewählten Plan weiter, nur noch in seiner Nähe – jedes Kriterium der Gruppen höchstens um
        /// die Toleranz der Stufe schlechter (mindestens ein Verstoß), seine Stärken gar nicht; die Grenze der Stufe beginnt mit den
        /// Plänen in dieser Nähe.
        /// </summary>
        private void StufeBeginnen(Grenzplan plan, int? hebel)
        {
            int stufe = steuerung.Stufe;
            double toleranz = e.Toleranz * Math.Pow(0.5, Math.Max(0, stufe - 2));

            // Seine Stärken bleiben: Was im gewählten Plan besser ist als in der bisherigen Empfehlung (ausgewogen), darf nicht
            // schlechter werden – sonst führte das Verfeinern zur alten Empfehlung zurück.
            Planguete? vorher = Bester(steuerung.Massstab with { Fokus = null })?.Guete;
            var neu = new Dictionary<Kostenkriterium, int>();
            foreach (Kostenkriterium k in Massstab.Gruppen.SelectMany(g => g.Kriterien))
            {
                int a = plan.Guete.Anzahl[(int)k];
                neu[k] = vorher is { } v && a < v.Anzahl[(int)k] ? a : a + Math.Max(1, (int)Math.Ceiling(a * toleranz));
            }

            Massstab massstab = steuerung.Massstab;
            IReadOnlyList<Grenzplan> bisher = grenze.Plaene;
            var frisch = new Paretofront();
            lock (sperre)
            {
                naehe = neu;
                gewaehlt = plan;
                grenze = frisch;
                kandidaten.ForEach(k => k.Neubeginnen(plan.Plan, plan.Guete));
            }

            frisch.Aufnehmen(plan.Plan, plan.Guete, massstab);
            foreach (Grenzplan p in bisher.Where(p => InNaehe(p.Guete)))
            {
                frisch.Aufnehmen(p.Plan, p.Guete, massstab);
            }

            Notieren(string.Create(CultureInfo.InvariantCulture, $"Stufe {stufe}: Klassenleiter wählt {Beschreiben(plan.Guete)}; Hebel {(hebel is int h ? Massstab.Gruppen[h].Hebel : "keiner (ausgewogen verfeinern)")}; Toleranz {toleranz:P0}"));
        }

        /// <summary>Ob ein Plan in der Nähe des gewählten liegt (vor der ersten Wahl: immer).</summary>
        private bool InNaehe(Planguete guete)
        {
            Dictionary<Kostenkriterium, int>? grenzen;
            lock (sperre)
            {
                grenzen = naehe;
            }

            return grenzen is null || grenzen.All(g => guete.Anzahl[(int)g.Key] <= g.Value);
        }

        /// <summary>Der Fokus des Klassenleiters rechnet (wieder) mit.</summary>
        private void FokusAktivieren(Massstab massstab)
        {
            if (massstab.Fokus is not int f)
            {
                return;
            }

            lock (sperre)
            {
                if (!kandidaten.Exists(k => k.Strikt && k.Gruppe == f) && kandidaten.Count > 0)
                {
                    (IReadOnlyList<Spiel> plan, Planguete guete) = kandidaten[0].Letzter;
                    kandidaten.Add(Fokuskandidat(f, plan, guete, strikt: true, glaetten));
                }
            }

            Notieren($"Klassenleiter: Fokus auf {Massstab.Gruppen[f].Name}");
        }

        /// <summary>Wo ein Kandidat beginnt: beim besten Plan der Grenze für seine Richtung, oder bei seinem letzten.</summary>
        private IReadOnlyList<Spiel> Start(Schnellkandidat kandidat, Massstab richtung)
        {
            (IReadOnlyList<Spiel> Plan, Planguete Guete) bester = kandidat.Letzter;
            foreach (Grenzplan p in grenze.Plaene.Where(p => richtung.IstBesser(p.Guete, bester.Guete)))
            {
                bester = (p.Plan, p.Guete);
            }

            return bester.Plan;
        }

        /// <summary>Eine Runde eines Kandidaten: Inseln und Teilneuplanung wie im Automodus, Pflicht und Wünsche festgelegt.</summary>
        private void Rechnen(Schnellkandidat kandidat, Dictionary<Kostenkriterium, int> grenzen, TimeSpan dauer, int kerne)
        {
            int samen = Interlocked.Increment(ref startwert);
            Massstab richtung = Richtung(kandidat, steuerung.Massstab);

            // Die Gruppe im Fokus wiegt dreifach (vorn in C, schwerer im Ziel von HiGHS) – wichtig, wo sie nicht in B stehen kann.
            Massstab gewichtet = kandidat is { Strikt: true, Gruppe: int fg } ? richtung.MitGewicht(fg, richtung.Gewichte[fg] * 3) : richtung;
            Stufeneinteilung einteilung = NachGewichten(kandidat.Einteilung, gewichtet);
            Optimierungseinstellungen einst = Optimierungseinstellungen.Standard with
            {
                Kerne = kerne,
                Inseln = Math.Min(4, Math.Max(2, kerne)),
                Automodus = true,
                AutoStufen = einteilung,
                AutoOhneGrundlauf = true,
                Startplan = Start(kandidat, richtung),
                StartplanInseln = Math.Min(4, Math.Max(2, kerne)),
                Startwert = samen,
            };
            var dauerUhr = Stopwatch.StartNew();
            using var optimierer = new Inseloptimierer(staffel, optionen, einst);
            Dictionary<Kostenkriterium, int> fest = new(grenzen);
            lock (sperre)
            {
                foreach ((Kostenkriterium k, int wert) in naehe ?? [])
                {
                    fest[k] = wert;
                }
            }

            optimierer.Festlegen(fest, "Einfacher Modus: Pflicht und Wünsche wie im Grundplan" + (fest.Count > grenzen.Count ? ", nahe am gewählten Plan" : string.Empty));

            // Pflicht ist bewiesen bestmöglich: nicht mehr drängen (die Runde gehört der Gruppe im Fokus und dem Glätten).
            if (schrankeA is int a)
            {
                optimierer.HighsEinspielen([], a, null);
            }

            using Teilneuplaner? neuplaner = loeser is null
                ? null
                : new Teilneuplaner(optimierer, staffel, optionen, loeser(samen), einteilung, grenzen, arbeiter: 1, cGewicht: Gewichtung(gewichtet));
            optimierer.Starten();
            neuplaner?.Starten();

            // Bis die Runde um ist – oder der Klassenleiter gewählt hat (dann beginnt die neue Stufe sofort).
            bool weiter = true;
            while (weiter)
            {
                weiter = dauerUhr.Elapsed < dauer && !steuerung.WahlOffen && !abbruch.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(250));
            }

            neuplaner?.Dispose();
            optimierer.Stoppen();
            IReadOnlyList<Spiel> plan = optimierer.BesterStand().Spiele;
            Planguete guete = Bewerten(plan);
            kandidat.Runde(plan, guete, dauerUhr.Elapsed.TotalSeconds);
            (IReadOnlyList<Spiel> Plan, Planguete Guete)? vorher = Bester(steuerung.Massstab);
            if (Aufnehmen(plan, guete, kandidat.Name))
            {
                kandidat.Erfolg(vorher is { } v && steuerung.Massstab.IstBesser(guete, v.Guete) ? 2 : 0.5);
            }
        }

        /// <summary>Die Gewichte der C-Kriterien für HiGHS nach dem Maßstab (Gruppen im Fokus stehen ohnehin in B).</summary>
        private Func<Kostenkriterium, double> Gewichtung(Massstab massstab)
        {
            int n = staffel.Mannschaften.Count;
            return k => massstab.Kriteriumsgewicht(k, n);
        }

        private Planguete Bewerten(IReadOnlyList<Spiel> plan) =>
            Planguete.Bewerten(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = plan }, optionen));

        /// <summary>Phase 1: der bisher beste Plan nach Pflicht und Wünschen (die Grenze gibt es erst mit dem Bezug).</summary>
        private void Vorlaeufig(IReadOnlyList<Spiel> plan, string herkunft)
        {
            if (plan.Count == 0)
            {
                return;
            }

            Planguete guete = Bewerten(plan);
            bool neu;
            lock (sperre)
            {
                neu = vorlaeufig is not { } b || Massstab.Standard.IstBesser(guete, b.Guete);
                if (neu)
                {
                    vorlaeufig = (plan, guete);
                }

                if (guete.Harte == 0 && ersterGueltiger is null)
                {
                    ersterGueltiger = uhr.Elapsed.TotalSeconds;
                }
            }

            if (neu)
            {
                melden(Stand($"Bester Plan ({herkunft}): {guete}"));
            }
        }

        /// <summary>Ein Plan aus dem Rennen: an die Grenze, wenn er in der Nähe des gewählten liegt und nicht dominiert ist.</summary>
        private bool Aufnehmen(IReadOnlyList<Spiel> plan, Planguete guete, string herkunft)
        {
            if (!InNaehe(guete) || !grenze.Aufnehmen(plan, guete, steuerung.Massstab))
            {
                return false;
            }

            melden(Stand($"An der Grenze ({herkunft}): {Beschreiben(guete)}"));
            return true;
        }

        /// <summary>Pflicht, Wünsche und je Gruppe der Wert (nach dem Maßstab des Augenblicks).</summary>
        private string Beschreiben(Planguete guete)
        {
            Massstab massstab = steuerung.Massstab;
            string gruppen = string.Join(" ", Enumerable.Range(0, Massstab.Gruppen.Count)
                .Where(g => massstab.Gruppe(guete, g) > 0)
                .Select(g => string.Create(CultureInfo.InvariantCulture, $"{Massstab.Gruppen[g].Name} {massstab.Gruppe(guete, g):0.00}")));
            return string.Create(CultureInfo.InvariantCulture, $"Pflicht {guete.Harte}·{guete.A} · Wünsche {guete.Wuensche} · Güte {massstab.Guete(guete):0.00} ({gruppen})");
        }

        private void Notieren(string text)
        {
            string zeile = string.Create(CultureInfo.InvariantCulture, $"[{uhr.Elapsed.TotalSeconds,6:0.0} s] {text}");
            lock (sperre)
            {
                protokoll.Add(zeile);
            }

            melden(Stand(text));
        }

        /// <summary>Der beste Plan für die Wahl des Klassenleiters: an der Grenze, sonst der vorläufige aus Phase 1.</summary>
        private (IReadOnlyList<Spiel> Plan, Planguete Guete)? Bester(Massstab massstab)
        {
            Grenzplan? bester = null;
            foreach (Grenzplan p in grenze.Plaene.Where(p => bester is null || massstab.IstBesser(p.Guete, bester.Guete)))
            {
                bester = p;
            }

            return bester is null ? vorlaeufig : (bester.Plan, bester.Guete);
        }

        private Schnellstand Stand(string text)
        {
            Massstab massstab = steuerung.Massstab;
            List<Schnellkandidat> liste;
            lock (sperre)
            {
                liste = [.. kandidaten];
            }

            (IReadOnlyList<Spiel> Plan, Planguete Guete)? bester = Bester(massstab);
            IReadOnlyList<Grenzplan> plaene = grenze.Plaene;
            return new Schnellstand(uhr.Elapsed.TotalSeconds, phase, text, bester, liste, plaene, massstab, steuerung.Stufe, Vorschlaege(plaene, massstab), Status(bester), bericht?.SchrankeA, bericht?.SchrankeB);
        }

        private Schnellergebnis Ergebnis((IReadOnlyList<Spiel> Plan, Planguete Guete)? grundplan, IReadOnlyList<Kriteriumsprobe> proben)
        {
            Massstab massstab = steuerung.Massstab;
            (IReadOnlyList<Spiel> Plan, Planguete Guete)? bester = Bester(massstab);
            List<Schnellkandidat> liste;
            lock (sperre)
            {
                liste = Rangfolge(kandidaten, massstab);
            }

            return new Schnellergebnis(
                bester?.Plan ?? [],
                bester?.Guete,
                grundplan,
                proben,
                liste,
                ersterGueltiger,
                aUndB,
                [.. protokoll],
                grenze.Plaene,
                massstab,
                steuerung.Stufe,
                Vorschlaege(grenze.Plaene, massstab),
                Status(bester),
                bericht?.SchrankeA,
                bericht?.SchrankeB);
        }

        /// <summary>Die Vorschläge (ab Phase 3); die Schwelle halbiert sich mit jeder Stufe.</summary>
        private IReadOnlyList<Vorschlag>? Vorschlaege(IReadOnlyList<Grenzplan> plaene, Massstab massstab)
        {
            Grenzplan? zuletzt;
            lock (sperre)
            {
                zuletzt = gewaehlt;
            }

            return phase < 3 ? null : Vorschlagsauswahl.Waehlen(plaene, massstab, e.Schwelle * Math.Pow(0.5, steuerung.Stufe - 1), zuletzt);
        }

        private IReadOnlyList<Kriteriumsstatus>? Status((IReadOnlyList<Spiel> Plan, Planguete Guete)? bester) =>
            bester is { } b ? Kriteriumsstatus.Bestimmen(b.Guete, bericht) : null;
    }
}
