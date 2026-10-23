// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Vergleich verschiedener Verfahren mit HiGHS (<c>andigen highsvergleich</c>): Wie schnell findet welches Verfahren einen Plan
/// mit höchstens so vielen A-Verstößen wie vorgegeben und möglichst wenigen B-Verstößen.
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>
    /// Nummer des Löser-Laufs für die Teilneuplanung: ab hier (so unterscheidet der Aufrufer Läufe der Teilneuplanung, deren
    /// Modellziel 20 · B + C ist, von denen für B).
    /// </summary>
    public const int TeilneuplanungsLauf = 100;

    /// <summary>Zeitgrenze für HiGHS je Ausschnitt der Teilneuplanung im Vergleich.</summary>
    private static readonly TimeSpan VergleichJeAusschnitt = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Rechnet ein Verfahren bis zur Zeitgrenze und meldet jeden besseren Plan (bewertet nach der Einteilung). Besser heißt: keine
    /// harten Fehler, A höchstens <paramref name="zielA"/>, dann weniger B-Verstöße, dann weniger A- und C-Verstöße.
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="verfahren">Das Verfahren.</param>
    /// <param name="zielA">So viele A-Verstöße darf der Plan haben (die Verfahren „Direkt“, „Portfolio“ usw. halten das im Modell fest).</param>
    /// <param name="dauer">Zeitgrenze für das ganze Verfahren.</param>
    /// <param name="laeufe">So viele Läufe von HiGHS gleichzeitig (Portfolio, Teilneuplanung, Kombination).</param>
    /// <param name="loeser">Der Löser je Lauf (Nummer des Laufs, z. B. für verschiedene Startwerte von HiGHS).</param>
    /// <param name="plan">Bekommt jeden besseren Plan mit seiner Bewertung und Herkunft.</param>
    /// <param name="melden">Meldet die Schritte des Verfahrens.</param>
    /// <param name="abbruch">Bricht ab.</param>
    public static void Vergleichen(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung, Highsverfahren verfahren, int zielA, TimeSpan dauer, int laeufe, Func<int, ISchrankenloeser> loeser, Action<IReadOnlyList<Spiel>, Stufenwert, string> plan, Action<string> melden, CancellationToken abbruch)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(einteilung);
        ArgumentNullException.ThrowIfNull(loeser);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(melden);
        RefPlan referenz = RefPlan.Laden(optionen.Rundenplanung == Rundenplanung.NurRueckrunde ? staffel : staffel with { BestehenderSpielplan = [] }, optionen);
        var d = new KernDefinition(referenz);
        if (Grund(d) is { } grund)
        {
            melden(grund);
            return;
        }

        var vergleich = new Vergleichslauf(staffel, optionen, einteilung.Vervollstaendigt(), zielA, dauer, Math.Max(1, laeufe), loeser, plan, melden, abbruch);
        new Ligaschranke(d).VergleichRechnen(vergleich, verfahren);
    }

    /// <summary>
    /// Teilneuplanung (Ziel 20 · B + C, A nicht schlechter): am besten Plan aller Läufe oder – mit <paramref name="linie"/> – am
    /// besten Plan einer Linie (ein langer Lauf von HiGHS und die Teilneuplanungen, die ihm folgen; so werden auch Pläne aus
    /// anderen Gegenden verfeinert, die nicht gleich besser sind als der beste). Beginnt erst mit einem Plan, der A einhält
    /// (sonst hielte sie das zu hohe A fest).
    /// </summary>
    private static void Teilneuplanen(Vergleichslauf v, int lauf, int? linie = null)
    {
        (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? Vorlage() => linie is int l ? v.LinieBester(l) : v.Bester;

        // Mit einem Plan, der A einhält – oder nach Stufe A mit dem besten A, das es gibt (ist das Ziel zu streng, begänne sie sonst nie).
        while (Vorlage() is not { } s0 || !(v.HaeltA(s0.Wert) || (v.StufeAFertig && v.Bester is { } b0 && s0.Wert.Harte == 0 && s0.Wert.A <= b0.Wert.A)))
        {
            if (v.Aus())
            {
                return;
            }

            Thread.Sleep(200);
        }

        (IReadOnlyList<Spiel> plan, _) = Vorlage()!.Value;
        string herkunft = linie is int nummer
            ? string.Create(CultureInfo.InvariantCulture, $"Teilneuplanung {lauf + 1} (Linie {nummer + 1})")
            : string.Create(CultureInfo.InvariantCulture, $"Teilneuplanung {lauf + 1}");
        var e = new Neuplanungseinstellungen(
            v.Rest,
            VergleichJeAusschnitt,
            Startwert: lauf + 1,
            Belegung: v.Belegung,
            Platz: lauf,
            Ausgangsplan: eigener => Vorlage() is { } b && v.IstBesser(b.Wert, eigener) ? b.Plan : null,
            Verbessert: (p, w) =>
            {
                if (linie is int l)
                {
                    v.LinieAnbieten(l, p, w);
                }

                v.Anbieten(p, w, herkunft);
            },
            Unterbrechen: v.Aus);
        Neuplanungsbericht n = Neuplanen(v.Staffel, plan, v.Optionen, v.Loeser(TeilneuplanungsLauf + lauf), v.Einteilung, e, abbruch: v.Abbruch);
        v.Melden(string.Create(CultureInfo.InvariantCulture, $"{herkunft}: {n.Schritte} Ausschnitte, {n.Angenommen} angenommen"));
    }

    private static void Gleichzeitig(IEnumerable<Action> arbeiten) => Task.WaitAll([.. arbeiten.Select(Task.Run)]);

    /// <summary>Eine Laufzeitregel, deren Höchstzeit die verbleibende Zeit nicht überschreitet.</summary>
    private static Laufzeitregel Begrenzt(Laufzeitregel regel, Vergleichslauf v) => regel with { Hoechstens = regel.Hoechstens < v.Rest ? regel.Hoechstens : v.Rest };

    private void VergleichRechnen(Vergleichslauf v, Highsverfahren verfahren)
    {
        // A1: Spiele ohne Termin (fast immer sofort 0), festgehalten; erst danach gelten die genauen Formen der Kriterien. Der
        // Plan aus A1 (mit den Hilfsspalten ergänzt) ist Startlösung – ohne sie fand HiGHS in Stufe A in 25 s nur A 103.
        List<(int Spalte, double Faktor)> a1 = ohneTermin.Select(u => (u.Spalte, 1.0)).ToList();
        (_, int? ohne, double[]? planA1, _) = Minimieren(modell, a1, v.Loeser(0), v.Rest, aufhoeren: v.Aus);
        if (ohne is not int h || planA1 is null)
        {
            v.Melden("A1: kein Plan gefunden.");
            return;
        }

        modell.Zeile(a1, double.NegativeInfinity, h);
        List<(int Spalte, double Faktor)> zielA = Stufensumme(v.Einteilung.A);
        List<(int Spalte, double Faktor)> zielB = Stufensumme(v.Einteilung.B);
        double[]? start = Einbetten(modell, Plan(planA1), Zusammenfassen(zielA.Concat(zielB)), v.Loeser(0));
        v.Melden(string.Create(CultureInfo.InvariantCulture, $"{v.Sekunden:0} s A1 = {h}; Modell {modell.Spalten} Spalten, {modell.Zeilen} Zeilen"));
        Anbieten(v, start, "A1");

        // Ohne Beweis von A: Ein Hilfswert zählt die A-Verstöße über dem Ziel, jeder davon wiegt 100 B-Verstöße (Ziel: B + 100 ·
        // max(0, A − Ziel)) – HiGHS rechnet A und B in einem Lauf gleich nach A1.
        Ganzzahlmodell gewichtet = modell.Kopie();
        int ueber = gewichtet.Zahl(0, 10_000);
        gewichtet.Zeile([(ueber, 1.0), .. zielA.Select(e => (e.Spalte, -e.Faktor))], -v.ZielA, double.PositiveInfinity);
        List<(int Spalte, double Faktor)> zielAB = [.. zielB, (ueber, 100.0)];
        double[]? startAB = start is null ? null : Einbetten(gewichtet, Plan(start), zielAB, v.Loeser(0));

        var regel = new Laufzeitregel(StandardZeitJeStufe, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(15));
        var geduldig = new Laufzeitregel(TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(30));
        switch (verfahren)
        {
            case Highsverfahren.Kette:
                Kette(v, zielA, zielB, start, regel, regel, null);
                break;
            case Highsverfahren.KetteGeduldig:
                Kette(v, zielA, zielB, start, geduldig, regel, null);
                break;
            case Highsverfahren.KetteLang:
                Kette(v, zielA, zielB, start, geduldig, regel, v.Dauer);
                break;
            case Highsverfahren.Direkt:
                Lang(v, gewichtet, zielAB, startAB, 0);
                break;
            case Highsverfahren.Portfolio:
                Gleichzeitig(Enumerable.Range(0, v.Laeufe).Select(i => (Action)(() => Lang(v, gewichtet, zielAB, startAB, i))));
                break;
            case Highsverfahren.Teilneuplanung:
                StufeA(v, zielA, start, geduldig);
                Gleichzeitig(Enumerable.Range(0, v.Laeufe).Select(i => (Action)(() => Teilneuplanen(v, i))));
                break;
            case Highsverfahren.Mehrfach:
                // Mehrere lange Läufe (Linien), gleichzeitig Stufe A; die übrigen Läufe planen Ausschnitte der Linien neu.
                int linien = Math.Max(2, v.Laeufe / 3);
                Gleichzeitig(Enumerable.Range(0, Math.Max(linien + 2, v.Laeufe)).Select(i => (Action)(() =>
                {
                    if (i < linien)
                    {
                        Lang(v, gewichtet, zielAB, startAB, i, i);
                    }
                    else if (i == linien)
                    {
                        StufeA(v, zielA, start, geduldig);
                    }
                    else
                    {
                        Teilneuplanen(v, i, (i - linien - 1) % linien);
                    }
                })));
                break;
            default:
                // Kombination: ein langer Lauf für A und B zusammen ab A1 (wie „Direkt“), gleichzeitig Stufe A und danach die
                // Teilneuplanung auf den übrigen Läufen – alle am selben besten Plan (der lange Lauf meldet jede bessere Lösung sofort).
                Gleichzeitig(Enumerable.Range(0, Math.Max(2, v.Laeufe)).Select(i => (Action)(() =>
                {
                    if (i == 0)
                    {
                        Lang(v, gewichtet, zielAB, startAB, 0);
                    }
                    else if (i == 1)
                    {
                        StufeA(v, zielA, start, geduldig);
                        Teilneuplanen(v, 1);
                    }
                    else
                    {
                        Teilneuplanen(v, i);
                    }
                })));
                break;
        }
    }

    /// <summary>Stufe A für sich (auf einer Kopie): der Startplan der Teilneuplanung, die A hält, aber nicht senkt.</summary>
    private void StufeA(Vergleichslauf v, List<(int Spalte, double Faktor)> zielA, double[]? start, Laufzeitregel regel)
    {
        (_, int? a, double[]? planA, _) = Minimieren(modell.Kopie(), zielA, v.Loeser(1), v.Rest, start, v.Aus, Begrenzt(regel, v));
        v.Melden(string.Create(CultureInfo.InvariantCulture, $"{v.Sekunden:0} s Stufe A: {a?.ToString(CultureInfo.InvariantCulture) ?? "–"}"));
        Anbieten(v, planA, "Stufe A");
        v.StufeAFertig = true;
    }

    /// <summary>Ein langer Lauf bis zum Ende; jede bessere Lösung wird sofort angeboten (auch seiner Linie).</summary>
    private void Lang(Vergleichslauf v, Ganzzahlmodell basis, List<(int Spalte, double Faktor)> ziel, double[]? start, int lauf, int? linie = null)
    {
        string herkunft = string.Create(CultureInfo.InvariantCulture, $"HiGHS {lauf + 1}");
        void Melden(double[]? w)
        {
            if (w is not null)
            {
                List<Spiel> p = Plan(w);
                Stufenwert wert = v.Bewerten(p);
                if (linie is int l)
                {
                    v.LinieAnbieten(l, p, wert);
                }

                v.Anbieten(p, wert, herkunft);
            }
        }

        (_, _, double[]? werte, _) = Minimieren(basis.Kopie(), ziel, v.Loeser(lauf), v.Rest, start, v.Aus, zwischen: Melden);
        Melden(werte);
        v.Melden(string.Create(CultureInfo.InvariantCulture, $"{v.Sekunden:0} s {herkunft} fertig"));
    }

    /// <summary>Die Summe der Verstöße einer Stufe im Modell (Kriterien, die das Modell nicht kennt, fehlen).</summary>
    private List<(int Spalte, double Faktor)> Stufensumme(IEnumerable<Kostenkriterium> kriterien) =>
        Zusammenfassen(kriterien.Select(k => Ziel(k)).OfType<List<(int Spalte, double Faktor)>>().SelectMany(z => z));

    /// <summary>Wie die Untergrenze: erst A (festgehalten beim gefundenen Wert), dann B.</summary>
    private void Kette(Vergleichslauf v, List<(int Spalte, double Faktor)> zielA, List<(int Spalte, double Faktor)> zielB, double[]? start, Laufzeitregel regelA, Laufzeitregel regel, TimeSpan? zeitB)
    {
        (int schranke, int? gefunden, double[]? werte, _) = Minimieren(modell, zielA, v.Loeser(0), v.Rest, start, v.Aus, Begrenzt(regelA, v));
        v.Melden(string.Create(CultureInfo.InvariantCulture, $"{v.Sekunden:0} s Stufe A: Grenze {schranke}, gefunden {gefunden?.ToString(CultureInfo.InvariantCulture) ?? "–"}"));
        Anbieten(v, werte, "Stufe A");
        if (gefunden is not int a)
        {
            return;
        }

        modell.Zeile(zielA, double.NegativeInfinity, a);
        (schranke, gefunden, double[]? b, _) = Minimieren(modell, zielB, v.Loeser(0), zeitB ?? v.Rest, werte, v.Aus, zeitB is null ? Begrenzt(regel, v) : null);
        string danach = zeitB is null ? " (danach rechnet HiGHS hier nicht weiter)" : string.Empty;
        v.Melden(string.Create(CultureInfo.InvariantCulture, $"{v.Sekunden:0} s Stufe B: Grenze {schranke}, gefunden {gefunden?.ToString(CultureInfo.InvariantCulture) ?? "–"}{danach}"));
        Anbieten(v, b, "Stufe B");
    }

    /// <summary>Ein Plan als Lösung des Modells (Ansetzungen fest, Hilfsspalten von HiGHS ergänzt); <c>null</c>, wenn er nicht passt.</summary>
    private double[]? Einbetten(Ganzzahlmodell basis, IReadOnlyList<Spiel> plan, List<(int Spalte, double Faktor)> ziel, ISchrankenloeser loeser)
    {
        double[] x = Abbilden(plan, out _);
        Ganzzahlmodell kopie = basis.Kopie();
        foreach (int spalte in termine.Select(t => t.Spalte).Concat(ohneTermin.Select(u => u.Spalte)))
        {
            kopie.Zeile([spalte], x[spalte], x[spalte]);
        }

        return Minimieren(kopie, ziel, loeser, TimeSpan.FromSeconds(30)).Werte;
    }

    private void Anbieten(Vergleichslauf v, double[]? werte, string herkunft)
    {
        if (werte is not null)
        {
            List<Spiel> p = Plan(werte);
            v.Anbieten(p, v.Bewerten(p), herkunft);
        }
    }

    /// <summary>Gemeinsamer Stand eines Vergleichslaufs (bester Plan aller Läufe, Zeit).</summary>
    private sealed class Vergleichslauf(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung, int zielA, TimeSpan dauer, int laeufe, Func<int, ISchrankenloeser> loeser, Action<IReadOnlyList<Spiel>, Stufenwert, string> plan, Action<string> melden, CancellationToken abbruch)
    {
        private readonly Stopwatch uhr = Stopwatch.StartNew();
        private readonly Lock sperre = new();
        private readonly Dictionary<int, (IReadOnlyList<Spiel> Plan, Stufenwert Wert)> linien = [];
        private (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? bester;

        public Staffel Staffel => staffel;

        public Berechnungsoptionen Optionen => optionen;

        public Stufeneinteilung Einteilung => einteilung;

        public int ZielA => zielA;

        public TimeSpan Dauer => dauer;

        public int Laeufe => laeufe;

        public CancellationToken Abbruch => abbruch;

        public Ausschnittsbelegung Belegung { get; } = new();

        /// <summary>Holt oder setzt einen Wert, der angibt, ob Stufe A fertig ist (dann beginnt die Teilneuplanung in jedem Fall).</summary>
        public bool StufeAFertig { get; set; }

        public double Sekunden => uhr.Elapsed.TotalSeconds;

        public TimeSpan Rest => dauer - uhr.Elapsed > TimeSpan.Zero ? dauer - uhr.Elapsed : TimeSpan.Zero;

        public (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? Bester
        {
            get
            {
                lock (sperre)
                {
                    return bester;
                }
            }
        }

        public bool Aus() => abbruch.IsCancellationRequested || uhr.Elapsed >= dauer;

        /// <summary>Gibt an, ob ein Plan keine harten Fehler hat und A höchstens beim Ziel liegt.</summary>
        public bool HaeltA(Stufenwert w) => w.Harte == 0 && w.A <= zielA;

        /// <summary>Der beste Plan einer Linie (eines langen Laufs und der Teilneuplanungen, die ihm folgen).</summary>
        public (IReadOnlyList<Spiel> Plan, Stufenwert Wert)? LinieBester(int linie)
        {
            lock (sperre)
            {
                return linien.TryGetValue(linie, out var b) ? b : null;
            }
        }

        public void LinieAnbieten(int linie, IReadOnlyList<Spiel> p, Stufenwert w)
        {
            lock (sperre)
            {
                if (!linien.TryGetValue(linie, out var b) || IstBesser(w, b.Wert))
                {
                    linien[linie] = (p, w);
                }
            }
        }

        public ISchrankenloeser Loeser(int lauf) => loeser(lauf);

        public void Melden(string text) => melden(text);

        public Stufenwert Bewerten(IReadOnlyList<Spiel> p) => einteilung.Zaehlen(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = p }, optionen));

        /// <summary>Keine harten Fehler, A höchstens beim Ziel, dann weniger B, dann weniger A, dann weniger C.</summary>
        public bool IstBesser(Stufenwert x, Stufenwert y) =>
            (x.Harte, Math.Max(0, x.A - zielA), x.B, x.A, x.CAnzahl).CompareTo((y.Harte, Math.Max(0, y.A - zielA), y.B, y.A, y.CAnzahl)) < 0;

        public void Anbieten(IReadOnlyList<Spiel> p, Stufenwert w, string herkunft)
        {
            lock (sperre)
            {
                if (bester is { } b && !IstBesser(w, b.Wert))
                {
                    return;
                }

                bester = (p, w);
            }

            plan(p, w, herkunft);
        }
    }
}
