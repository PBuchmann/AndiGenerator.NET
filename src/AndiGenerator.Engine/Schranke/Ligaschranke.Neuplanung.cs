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
/// Teilneuplanung (MIGRATIONSPLAN O7 Schritt 3, „LNS“): Vom gegebenen Plan bleibt der größte Teil fest; ein Ausschnitt wird mit
/// HiGHS neu geplant – alle Spiele einiger Mannschaften (zufällig gewählt oder gezielt die mit den meisten C-Verstößen) oder
/// alle Spiele eines Zeitfensters von einigen Wochen. Art und Größe, die zuletzt die meisten Erfolge je Sekunde brachten, werden
/// öfter gewählt.
/// Bedingung: die Summen der Stufen A und B werden nicht schlechter; Ziel: weniger Verstöße in C – die Spieltage genau wie die
/// Bewertung (wenn alle Spiele einen Termin haben), Abstand Heim/Auswärts, Setzliste und – bei Koppelterminen – die Pausen genähert
/// (Abstand und Setzliste auf Wunsch genau, siehe <see cref="Neuplanungseinstellungen"/>). Ob es wirklich besser ist, entscheidet
/// die echte Bewertung (auch die Kriterien, die das Modell nicht kennt, und die Ausreißer).
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>So viele Schritte behält das Protokoll der Teilneuplanung höchstens.</summary>
    private const int ProtokollHoechstens = 200;

    /// <summary>
    /// Gewicht eines B-Verstoßes im Ziel der Teilneuplanung (C zählt 1): Ein Ausschnitt senkt zuerst B, dann C – so wie der
    /// Automodus vergleicht. Vorher zählte nur C, und B wurde nie besser als im Ausgangsplan.
    /// </summary>
    private const int GewichtB = 20;

    /// <summary>
    /// Je Mannschaft die Verstöße in C eines Plans (nach der Kostenfunktion; je Plan einmal berechnet) – für gezielte Ausschnitte
    /// (<see cref="Neuplanungseinstellungen.Verstoesse"/>).
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="einteilung">Die Einteilung (ihre Stufe C zählt).</param>
    /// <returns>Die Funktion.</returns>
    public static Func<IReadOnlyList<Spiel>, IReadOnlyDictionary<string, int>> VerstoesseInC(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung)
    {
        ArgumentNullException.ThrowIfNull(einteilung);
        int[] c = einteilung.Vervollstaendigt().C.Select(k => (int)k).Where(k => k < Berechnungsoptionen.AnzahlKostenarten).ToArray();
        var sperre = new Lock();
        (IReadOnlyList<Spiel>? Plan, IReadOnlyDictionary<string, int> Je) zuletzt = (null, new Dictionary<string, int>());
        return plan =>
        {
            lock (sperre)
            {
                if (ReferenceEquals(zuletzt.Plan, plan))
                {
                    return zuletzt.Je;
                }
            }

            Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = plan }, optionen);
            Dictionary<string, int> je = bewertung.Mannschaften.ToDictionary(m => m.Name, m => c.Sum(k => Math.Max(0, m.JeKostenart[k].Anzahl)));
            lock (sperre)
            {
                zuletzt = (plan, je);
            }

            return je;
        };
    }

    /// <summary>Plant Ausschnitte des Plans neu, solange Zeit ist.</summary>
    /// <param name="staffel">Die Staffel (bei „nur Rückrunde“ mit der Vorrunde als bestehendem Plan).</param>
    /// <param name="plan">Der Ausgangsplan.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="loeser">Der Löser (HiGHS).</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="einstellungen">Zeiten und Größe der Ausschnitte.</param>
    /// <param name="melden">Meldet jeden Schritt, oder <c>null</c>.</param>
    /// <param name="abbruch">Beendet die Neuplanung nach dem laufenden Schritt.</param>
    /// <returns>Der Bericht mit dem besten Plan.</returns>
    public static Neuplanungsbericht Neuplanen(Staffel staffel, IReadOnlyList<Spiel> plan, Berechnungsoptionen optionen, ISchrankenloeser loeser, Stufeneinteilung einteilung, Neuplanungseinstellungen einstellungen, Action<string>? melden = null, CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentNullException.ThrowIfNull(loeser);
        ArgumentNullException.ThrowIfNull(einteilung);
        ArgumentNullException.ThrowIfNull(einstellungen);
        RefPlan referenz = RefPlan.Laden(optionen.Rundenplanung == Rundenplanung.NurRueckrunde ? staffel : staffel with { BestehenderSpielplan = [] }, optionen);
        var d = new KernDefinition(referenz);
        einteilung = einteilung.Vervollstaendigt();
        Stufenwert wert = einteilung.Zaehlen(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = plan }, optionen));
        if (Grund(d) is { } grund)
        {
            return new Neuplanungsbericht(plan, wert, wert, 0, 0, [grund]);
        }

        return new Ligaschranke(d).Neuplanung(staffel, plan, optionen, loeser, einteilung, einstellungen, melden ?? (_ => { }), abbruch);
    }

    /// <summary>Warum es für diese Staffel kein Modell gibt; <c>null</c>, wenn es eines gibt.</summary>
    private static string? Grund(KernDefinition d)
    {
        if (d.Rundenplanung == Rundenplanung.Corona)
        {
            return "Für die Corona-Rückrunde gibt es die Schranke nicht.";
        }

        if (d.Rundenplanung != Rundenplanung.Beide && d.Doppelrunde)
        {
            return "Für eine halbe Doppelrunde gibt es die Schranke noch nicht.";
        }

        return d.Vorgegeben.Length > 0 ? "Vorgegebene Spiele werden von der Schranke noch nicht berücksichtigt." : null;
    }

    /// <summary>Der Wert eines Ziels in einer Lösung (Spalten, die die Lösung noch nicht kennt, zählen 0).</summary>
    private static double Wert(List<(int Spalte, double Faktor)> ziel, double[] werte) => ziel.Sum(e => e.Spalte < werte.Length ? e.Faktor * werte[e.Spalte] : 0);

    /// <summary>
    /// Art und Größe des nächsten Ausschnitts: meist die mit den meisten Erfolgen (C-Verstöße weniger) je Sekunde, jedes fünfte Mal
    /// eine zufällige Art in der kleinsten Größe (sonst bliebe die Teilneuplanung bei dem, was am Anfang zufällig Erfolg hatte;
    /// große Ausschnitte kosten viel Zeit und bringen selten etwas). Jede beginnt mit 0,3 Erfolgen und zwei Versuchen bis zur
    /// Zeitgrenze; größer wird eine Art erst, wenn die nächstkleinere Größe schon versucht wurde.
    /// </summary>
    private static (int Art, int Groesse) Waehlen(Dictionary<(int Art, int Groesse), (double Versuche, double Erfolge, double Sekunden)> statistik, int arten, int kleinste, int hoechstens, double sekundenJeVersuch, Random zufall)
    {
        var moeglich = new List<((int, int) Wahl, double Wert)>();
        for (int art = 0; art < arten; art++)
        {
            for (int g = kleinste; g <= hoechstens; g++)
            {
                (double versuche, double erfolge, double sekunden) = statistik.GetValueOrDefault((art, g));
                moeglich.Add(((art, g), (erfolge + 0.3) / (sekunden + (2 * sekundenJeVersuch * g / kleinste))));
                if (versuche <= 0)
                {
                    break;
                }
            }
        }

        return zufall.NextDouble() < 0.2 ? (zufall.Next(arten), kleinste) : moeglich.MaxBy(m => m.Wert).Wahl;
    }

    /// <summary>
    /// Ein Versuch dieser Art und Größe; alles Bisherige zählt dabei etwas weniger (nach einer Minute Rechenzeit halb so viel –
    /// Erfolge werden mit der Zeit seltener, und was eben nichts brachte, kann später wieder etwas bringen).
    /// </summary>
    private static void Erfassen(Dictionary<(int Art, int Groesse), (double Versuche, double Erfolge, double Sekunden)> statistik, (int Art, int Groesse) wahl, double sekunden)
    {
        double vergessen = Math.Pow(0.5, sekunden / 60);
        foreach ((int, int) k in statistik.Keys.ToArray())
        {
            (double v, double erf, double sek) = statistik[k];
            statistik[k] = (v * vergessen, erf * vergessen, sek * vergessen);
        }

        (double versuche, double erfolge, double zeit) = statistik.GetValueOrDefault(wahl);
        statistik[wahl] = (versuche + 1, erfolge, zeit + sekunden);
    }

    private Neuplanungsbericht Neuplanung(Staffel staffel, IReadOnlyList<Spiel> plan, Berechnungsoptionen optionen, ISchrankenloeser loeser, Stufeneinteilung einteilung, Neuplanungseinstellungen e, Action<string> melden, CancellationToken abbruch)
    {
        // Das Protokoll behält nur die letzten Schritte (die Teilneuplanung läuft im Automodus bis zum Ende der Generierung).
        var protokoll = new List<string>();
        void Melden(string text)
        {
            protokoll.Add(text);
            if (protokoll.Count > ProtokollHoechstens)
            {
                protokoll.RemoveRange(0, protokoll.Count - ProtokollHoechstens);
            }

            melden(text);
        }

        bool Aufhoeren() => abbruch.IsCancellationRequested || (e.Unterbrechen?.Invoke() ?? false);
        Stufenwert Bewerten(IReadOnlyList<Spiel> p) => e.Bewerten?.Invoke(p)
            ?? einteilung.Zaehlen(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = p }, optionen));

        // Die Ziele: jedes Kriterium genau einmal ins Modell (die Bausteine legen Hilfsspalten an).
        List<(int Spalte, double Faktor)> ohne = ohneTermin.Select(u => (u.Spalte, 1.0)).ToList();

        // A und B als Summe je Stufe (wie der Automodus vergleicht: innerhalb einer Stufe zählt jeder Verstoß gleich). Abstand H/A
        // und Setzliste bleiben dabei außen vor (genau machen sie die Ausschnitte für HiGHS unlösbar schwer, genähert verbieten
        // sie echte Verbesserungen): Ob A und B wirklich nicht schlechter werden, entscheidet die echte Bewertung.
        List<(int Spalte, double Faktor)> Stufenziel(IEnumerable<Kostenkriterium> kriterien) => Zusammenfassen(kriterien
            .Where(k => k is not (Kostenkriterium.AbstandHeimAuswaerts or Kostenkriterium.Setzliste))
            .Select(k => Ziel(k, naeherung: true)).OfType<List<(int Spalte, double Faktor)>>().SelectMany(z => z));
        List<List<(int Spalte, double Faktor)>> a = [ohne, Stufenziel(einteilung.A)];
        List<List<(int Spalte, double Faktor)>> b = [Stufenziel(einteilung.B)];
        List<List<(int Spalte, double Faktor)>> c = [.. einteilung.C
            .Select(k => Ziel(k, naeherung: true)).OfType<List<(int Spalte, double Faktor)>>().Select(Zusammenfassen)];

        // Der Ausgangsplan im Modell: Ansetzungen festlegen (je Ansetzungsspalte eine Zeile), Hilfsspalten ergänzen.
        double[] x = Abbilden(plan, out int fremd);
        if (fremd > 0)
        {
            Melden($"{fremd} Spiele des Plans liegen auf keinem Wunschtermin (im Modell ohne Termin).");
        }

        int[] ansetzung = [.. termine.Select(t => t.Spalte), .. ohneTermin.Select(u => u.Spalte)];
        int[] zeile = new int[ansetzung.Length];
        for (int i = 0; i < ansetzung.Length; i++)
        {
            zeile[i] = modell.Zeilen;
            modell.Zeile([ansetzung[i]], x[ansetzung[i]], x[ansetzung[i]]);
        }

        List<(int Spalte, double Faktor)> alles = Zusammenfassen(a.Concat(b).Concat(c).SelectMany(z => z));
        (_, _, double[]? aktuell, _) = Minimieren(modell, alles, loeser, e.JeSchritt);
        Stufenwert vorher = Bewerten(plan);
        if (aktuell is null)
        {
            Melden("Der Plan passt nicht ins Modell (keine Lösung beim Ergänzen der Hilfsspalten).");
            return new Neuplanungsbericht(plan, vorher, vorher, 0, 0, protokoll);
        }

        // Bedingungen: A und B als Summe nicht schlechter als im Ausgangsplan. Ziel: erst B, dann C.
        List<List<(int Spalte, double Faktor)>> bedingt = [.. a.Concat(b)];
        int ersteBedingung = modell.Zeilen;
        foreach (List<(int Spalte, double Faktor)> z in bedingt)
        {
            modell.Zeile(z, double.NegativeInfinity, Math.Round(Wert(z, aktuell)));
        }

        // Festlegung des Staffelleiters: je Kriterium höchstens so viele Verstöße (soweit das Modell es kennt).
        foreach ((Kostenkriterium k, int grenze) in e.Obergrenzen ?? new Dictionary<Kostenkriterium, int>())
        {
            if (Ziel(k) is { Count: > 0 } z)
            {
                modell.Zeile(Zusammenfassen(z), double.NegativeInfinity, grenze);
            }
        }

        string Werte(List<List<(int Spalte, double Faktor)>> stufe) =>
            string.Join(" + ", stufe.Select(z => Wert(z, aktuell!).ToString("0", CultureInfo.InvariantCulture)));
        List<(int Spalte, double Faktor)> ziel = Zusammenfassen(b.SelectMany(z => z).Select(e => (e.Spalte, e.Faktor * GewichtB)).Concat(c.SelectMany(z => z)));
        Melden(string.Create(CultureInfo.InvariantCulture, $"Start: {vorher}, Modellziel {Wert(ziel, aktuell):0} (im Modell A {Werte(a)}; B {Werte(b)}; C {Werte(c)}; {modell.Spalten} Spalten, {modell.Zeilen} Zeilen)"));

        var zufall = new Random(e.Startwert);
        var uhr = Stopwatch.StartNew();
        IReadOnlyList<Spiel> besterPlan = plan;
        Stufenwert bester = vorher;
        int schritte = 0;
        int angenommen = 0;

        // Ein besserer Plan von außen (z. B. der Inseln) wird neuer Ausgangsplan: Ansetzungen festlegen, Hilfsspalten ergänzen,
        // Bedingungen auf seine Werte setzen. Passt er nicht ins Modell, bleibt der bisherige.
        bool Wechseln(IReadOnlyList<Spiel> neu)
        {
            double[] y = Abbilden(neu, out _);
            for (int i = 0; i < ansetzung.Length; i++)
            {
                modell.ZeileGrenzen(zeile[i], y[ansetzung[i]], y[ansetzung[i]]);
            }

            for (int r = 0; r < bedingt.Count; r++)
            {
                modell.ZeileGrenzen(ersteBedingung + r, double.NegativeInfinity, double.PositiveInfinity);
            }

            (_, _, double[]? werte, _) = Minimieren(modell, alles, loeser, e.JeSchritt, aufhoeren: Aufhoeren);
            double[] gilt = werte ?? aktuell;
            for (int r = 0; r < bedingt.Count; r++)
            {
                modell.ZeileGrenzen(ersteBedingung + r, double.NegativeInfinity, Math.Round(Wert(bedingt[r], gilt)));
            }

            if (werte is null)
            {
                return false;
            }

            aktuell = werte;
            besterPlan = neu;
            bester = Bewerten(neu);
            return true;
        }

        // Gezielt: je Art (zufällige Mannschaften, Zeitfenster, Mannschaften mit den meisten C-Verstößen) und Größe Versuche,
        // Erfolge und Sekunden (ältere zählen allmählich weniger); gewählt wird, was die meisten Erfolge je Sekunde bringt. Sonst
        // nur zufällige Mannschaften, und die Ausschnitte wachsen, wenn eine Größe eine Weile nichts mehr bringt.
        var statistik = new Dictionary<(int Art, int Groesse), (double Versuche, double Erfolge, double Sekunden)>();
        int artenzahl = e.Verstoesse is null ? 2 : 3;
        int hoechstens = Math.Max(e.Mannschaften, d.N / 2);
        int groesse = e.Mannschaften;
        int gewaehlt = 0;
        void Freigeben(HashSet<int> frei)
        {
            for (int i = 0; i < ansetzung.Length; i++)
            {
                double v = Math.Round(aktuell[ansetzung[i]]);
                modell.ZeileGrenzen(zeile[i], frei.Contains(ansetzung[i]) ? 0 : v, frei.Contains(ansetzung[i]) ? 1 : v);
            }
        }

        while (uhr.Elapsed < e.Gesamt && !abbruch.IsCancellationRequested)
        {
            e.Belegung?.Belegen(e.Platz, []);
            if (e.Ausgangsplan?.Invoke(bester) is { } neuer)
            {
                bool gewechselt = Wechseln(neuer);
                Melden(gewechselt ? $"Neuer Ausgangsplan von außen: {bester}" : "Neuer Ausgangsplan passt nicht ins Modell – es geht mit dem bisherigen weiter.");
                if (gewechselt)
                {
                    e.Ausschnitt?.Invoke(new Ausschnittsmeldung(0, Ausschnittsergebnis.Ausgangsplan, bester));
                    groesse = e.Mannschaften;
                }
            }

            if (abbruch.IsCancellationRequested)
            {
                break;
            }

            schritte++;
            (gewaehlt, groesse) = Waehlen(statistik, artenzahl, e.Mannschaften, hoechstens, e.JeSchritt.TotalSeconds, zufall);
            HashSet<int> belegt = e.Belegung is null ? [] : [.. e.Belegung.Andere(e.Platz).Select(n => Array.IndexOf(d.Name, n)).Where(t => t >= 0)];
            (HashSet<int> frei, string ausschnitt, int art, int[] wahl) = AusschnittBilden(zufall, gewaehlt, groesse, besterPlan, e.Verstoesse, belegt);
            e.Belegung?.Belegen(e.Platz, wahl.Select(t => d.Name[t]));
            Freigeben(frei);
            e.Ausschnitt?.Invoke(new Ausschnittsmeldung(groesse, Ausschnittsergebnis.Laeuft, bester));
            var schritt = Stopwatch.StartNew();
            double alt = Wert(ziel, aktuell);
            (int grenze, _, double[]? werte, _) = Minimieren(modell, ziel, loeser, e.JeSchritt * groesse / e.Mannschaften, aktuell, Aufhoeren);
            double neu = werte is null ? alt : Wert(ziel, werte);

            // Ist der beste Plan inzwischen ein anderer (z. B. von den Inseln oder einer anderen Teilneuplanung), wäre das
            // Ergebnis verloren: Derselbe Ausschnitt wird am neuen Plan nachgerechnet – er war ja erfolgversprechend.
            if (werte is not null && neu <= alt - 0.5 && e.Ausgangsplan?.Invoke(bester) is { } spaeter)
            {
                if (!Wechseln(spaeter))
                {
                    werte = null;
                }
                else
                {
                    Freigeben(frei);
                    alt = Wert(ziel, aktuell);
                    (grenze, _, werte, _) = Minimieren(modell, ziel, loeser, e.JeSchritt * groesse / e.Mannschaften, aktuell, Aufhoeren);
                    neu = werte is null ? alt : Wert(ziel, werte);
                    Melden($"Ausgangsplan inzwischen überholt – Ausschnitt am neuen Plan nachgerechnet: {bester}");
                }
            }

            string zeit = string.Create(CultureInfo.InvariantCulture, $"{schritt.Elapsed.TotalSeconds:0.0} s, im Modell mindestens {grenze}");
            Erfassen(statistik, (art, groesse), schritt.Elapsed.TotalSeconds);
            if (werte is null || neu > alt - 0.5)
            {
                Melden(string.Create(CultureInfo.InvariantCulture, $"{schritte}: {ausschnitt} ({frei.Count} Spalten frei, {zeit}): {(werte is null ? "keine Lösung" : "nichts Besseres")}"));
                e.Ausschnitt?.Invoke(new Ausschnittsmeldung(groesse, Ausschnittsergebnis.NichtsBesseres, bester));
                continue;
            }

            // Die echte Bewertung entscheidet (auch über Kriterien, die das Modell nicht kennt).
            List<Spiel> kandidat = Plan(werte);
            Stufenwert w = Bewerten(kandidat);
            bool besser = e.Besser?.Invoke(w, bester) ?? Stufenvergleich.Besser(w, bester);
            Melden(string.Create(CultureInfo.InvariantCulture, $"{schritte}: {ausschnitt} ({frei.Count} Spalten frei, {zeit}): Modell {alt:0} → {neu:0}, {w} – {(besser ? "angenommen" : "abgelehnt")}"));
            if (besser)
            {
                // Als Erfolg zählt, wie viele Verstöße weniger (B gewichtet, mindestens einer): Große Schritte wiegen mehr als kleine.
                double gewinn = Math.Max(1, ((bester.B - w.B) * GewichtB) + bester.CAnzahl - w.CAnzahl);
                aktuell = werte;
                besterPlan = kandidat;
                bester = w;
                angenommen++;
                (double v, double erfolge, double sek) = statistik[(art, groesse)];
                statistik[(art, groesse)] = (v, erfolge + gewinn, sek);

                // Gleich weitergeben (z. B. an den Automodus), nicht erst am Ende.
                e.Verbessert?.Invoke(kandidat, w);
            }
            else
            {
                e.Abgelehnt?.Invoke(kandidat);
            }

            e.Ausschnitt?.Invoke(new Ausschnittsmeldung(groesse, besser ? Ausschnittsergebnis.Besser : Ausschnittsergebnis.Abgelehnt, bester));
        }

        return new Neuplanungsbericht(besterPlan, vorher, bester, schritte, angenommen, protokoll);
    }

    /// <summary>Ein Plan als Werte der Ansetzungsspalten (Spiel auf Wunschtermin bzw. ohne Termin).</summary>
    private double[] Abbilden(IReadOnlyList<Spiel> plan, out int fremd)
    {
        double[] x = new double[modell.Spalten];
        var vergeben = new HashSet<int>();
        fremd = 0;
        foreach (Spiel spiel in plan.Where(s => s.Zeitpunkt is not null))
        {
            int h = Array.IndexOf(d.Name, spiel.Heim);
            int g = Array.IndexOf(d.Name, spiel.Gast);
            double datum = DelphiDatum.Wert(spiel.Zeitpunkt!.Value);
            if (Array.Exists(feste, f => f.Heim == h && f.Gast == g && Math.Abs(f.Datum - datum) < 1e-4))
            {
                continue;
            }

            int i = termine.FindIndex(t => d.SpielHeim[t.Spiel] == h && d.SpielGast[t.Spiel] == g
                && Math.Abs(d.WunschDatum[t.Wunsch] - datum) < 1e-4 && !vergeben.Contains(Gruppe(t.Spiel)));
            if (i < 0)
            {
                fremd++;
                continue;
            }

            vergeben.Add(Gruppe(termine[i].Spiel));
            x[termine[i].Spalte] = 1;
        }

        foreach ((int spalte, _) in ohneTermin.Where(u => !vergeben.Contains(Gruppe(u.Spiel))))
        {
            x[spalte] = 1;
        }

        return x;
    }

    /// <summary>
    /// Wofür eine Ansetzung steht: bei Hin- und Rückrunde das Spiel selbst, sonst die Paarung (die Richtung ist frei).
    /// </summary>
    private int Gruppe(int spiel)
    {
        if (d.Rundenplanung == Rundenplanung.Beide)
        {
            return spiel;
        }

        int h = d.SpielHeim[spiel];
        int g = d.SpielGast[spiel];
        return d.SpielHeim.Length + (Math.Min(h, g) * d.N) + Math.Max(h, g);
    }

    /// <summary>
    /// Bildet den Ausschnitt der gewählten Art: 0 = zufällige Mannschaften, 1 = Zeitfenster, 2 = Mannschaften mit den meisten
    /// C-Verstößen (gewichtet gezogen).
    /// </summary>
    private (HashSet<int> Frei, string Text, int Art, int[] Wahl) AusschnittBilden(
        Random zufall,
        int art,
        int groesse,
        IReadOnlyList<Spiel> plan,
        Func<IReadOnlyList<Spiel>, IReadOnlyDictionary<string, int>>? verstoesse,
        HashSet<int> belegt)
    {
        return art switch
        {
            1 => Zeitfenster(zufall, groesse) is { Frei.Count: > 0 } fenster ? (fenster.Frei, fenster.Text, 1, []) : Mit(MannschaftenFrei(zufall, groesse, belegt), 0),
            2 when verstoesse is not null => Mit(MannschaftenMitVerstoessen(zufall, groesse, verstoesse(plan), belegt), 2),
            _ => Mit(MannschaftenFrei(zufall, groesse, belegt), 0),
        };

        static (HashSet<int>, string, int, int[]) Mit((HashSet<int> Frei, string Text, int[] Wahl) a, int art) => (a.Frei, a.Text, art, a.Wahl);
    }

    /// <summary>
    /// Ausschnitt: alle Spiele eines Zeitfensters (Wunschtermine darin frei, die übrigen fest) – etwa so viele Wochen der Saison,
    /// wie <paramref name="groesse"/> Mannschaften Anteil an der Staffel haben (mindestens drei).
    /// </summary>
    private (HashSet<int> Frei, string Text) Zeitfenster(Random zufall, int groesse)
    {
        double[] daten = termine.Select(t => d.WunschDatum[t.Wunsch]).Distinct().Order().ToArray();
        if (daten.Length == 0)
        {
            return ([], string.Empty);
        }

        double erstes = daten[0];
        double dauer = Math.Max(21, (daten[^1] - erstes) * groesse / Math.Max(1, d.N));
        double von = erstes + (zufall.NextDouble() * Math.Max(0, daten[^1] - erstes - dauer));
        double bis = von + dauer;
        var frei = termine.Where(t => d.WunschDatum[t.Wunsch] >= von && d.WunschDatum[t.Wunsch] <= bis).Select(t => t.Spalte).ToHashSet();
        return (frei, string.Create(CultureInfo.InvariantCulture, $"Zeitfenster {DelphiDatum.ZuDateTime(von):dd.MM.} – {DelphiDatum.ZuDateTime(bis):dd.MM.}"));
    }

    /// <summary>Ausschnitt: Mannschaften gewichtet nach ihren C-Verstößen gezogen (mehr Verstöße, größere Chance).</summary>
    private (HashSet<int> Frei, string Text, int[] Wahl) MannschaftenMitVerstoessen(Random zufall, int anzahl, IReadOnlyDictionary<string, int> verstoesse, HashSet<int> belegt)
    {
        List<int> uebrig = Waehlbar(anzahl, belegt);
        var wahl = new List<int>();
        while (wahl.Count < Math.Min(anzahl, d.N))
        {
            double[] gewicht = uebrig.Select(t => 0.5 + verstoesse.GetValueOrDefault(d.Name[t])).ToArray();
            double wurf = zufall.NextDouble() * gewicht.Sum();
            int i = 0;
            while (i < gewicht.Length - 1 && wurf >= gewicht[i])
            {
                wurf -= gewicht[i];
                i++;
            }

            wahl.Add(uebrig[i]);
            uebrig.RemoveAt(i);
        }

        var frei = termine.Where(t => wahl.Contains(d.SpielHeim[t.Spiel]) || wahl.Contains(d.SpielGast[t.Spiel])).Select(t => t.Spalte).ToHashSet();
        frei.UnionWith(ohneTermin.Where(u => wahl.Contains(d.SpielHeim[u.Spiel]) || wahl.Contains(d.SpielGast[u.Spiel])).Select(u => u.Spalte));
        return (frei, "C-Verstöße: Mannschaften " + string.Join(", ", wahl.Select(t => d.Name[t])), [.. wahl]);
    }

    /// <summary>Ausschnitt: alle Spiele einiger Mannschaften auf allen ihren Terminen.</summary>
    private (HashSet<int> Frei, string Text, int[] Wahl) MannschaftenFrei(Random zufall, int anzahl, HashSet<int> belegt)
    {
        int[] wahl = Waehlbar(anzahl, belegt).OrderBy(_ => zufall.Next()).Take(Math.Min(anzahl, d.N)).ToArray();
        var frei = termine.Where(t => wahl.Contains(d.SpielHeim[t.Spiel]) || wahl.Contains(d.SpielGast[t.Spiel])).Select(t => t.Spalte).ToHashSet();
        frei.UnionWith(ohneTermin.Where(u => wahl.Contains(d.SpielHeim[u.Spiel]) || wahl.Contains(d.SpielGast[u.Spiel])).Select(u => u.Spalte));
        return (frei, "Mannschaften " + string.Join(", ", wahl.Select(t => d.Name[t])), wahl);
    }

    /// <summary>Die Mannschaften, aus denen gewählt wird: ohne die, die andere gerade neu planen, wenn genug übrig bleiben.</summary>
    private List<int> Waehlbar(int anzahl, HashSet<int> belegt)
    {
        List<int> frei = Enumerable.Range(0, d.N).Where(t => !belegt.Contains(t)).ToList();
        return frei.Count >= anzahl ? frei : [.. Enumerable.Range(0, d.N)];
    }
}
