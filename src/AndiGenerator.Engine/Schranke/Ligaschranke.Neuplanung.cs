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
/// Teilneuplanung (MIGRATIONSPLAN O7 Schritt 3, „LNS“): Vom gegebenen Plan bleibt der größte Teil fest; ein Ausschnitt – alle
/// Spiele einiger Mannschaften; bringt eine Größe eine Weile nichts mehr, eine Mannschaft mehr – wird mit HiGHS neu geplant.
/// Bedingung: die Summen der Stufen A und B werden nicht schlechter; Ziel: weniger Verstöße in C. Ob es wirklich besser ist,
/// entscheidet die echte Bewertung (auch die Kriterien, die das Modell nicht kennt).
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>So viele Schritte behält das Protokoll der Teilneuplanung höchstens.</summary>
    private const int ProtokollHoechstens = 200;

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

    private static double Wert(List<(int Spalte, double Faktor)> ziel, double[] werte) => ziel.Sum(e => e.Faktor * werte[e.Spalte]);

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

        // A und B als Summe je Stufe (wie der Automodus vergleicht: innerhalb einer Stufe zählt jeder Verstoß gleich).
        List<List<(int Spalte, double Faktor)>> a = [ohne, Zusammenfassen(einteilung.A.Select(k => Ziel(k)).OfType<List<(int Spalte, double Faktor)>>().SelectMany(z => z))];
        List<List<(int Spalte, double Faktor)>> b = [Zusammenfassen(einteilung.B.Select(k => Ziel(k)).OfType<List<(int Spalte, double Faktor)>>().SelectMany(z => z))];
        List<List<(int Spalte, double Faktor)>> c = [.. einteilung.C
            .Where(k => e.MitWechsel || k != Kostenkriterium.WechselHeimAuswaerts)
            .Where(k => e.MitAbstand || k != Kostenkriterium.AbstandHeimAuswaerts)
            .Where(k => e.MitSetzliste || k != Kostenkriterium.Setzliste)
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

        // Bedingungen: A und B als Summe nicht schlechter als im Ausgangsplan. Ziel: C.
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
        List<(int Spalte, double Faktor)> ziel = Zusammenfassen(c.SelectMany(z => z));
        Melden(string.Create(CultureInfo.InvariantCulture, $"Start: {vorher}, Modellziel {Wert(ziel, aktuell):0} (im Modell A {Werte(a)}; B {Werte(b)}; C {Werte(c)})"));

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

        // Ausschnitte wachsen, wenn eine Größe eine Weile nichts mehr bringt (danach wieder von vorn).
        int hoechstens = Math.Max(e.Mannschaften, d.N / 2);
        int groesse = e.Mannschaften;
        int ohneErfolg = 0;
        int geduld = Math.Max(4, d.N / 2);
        while (uhr.Elapsed < e.Gesamt && !abbruch.IsCancellationRequested)
        {
            if (e.Ausgangsplan?.Invoke(bester) is { } neuer)
            {
                bool gewechselt = Wechseln(neuer);
                Melden(gewechselt ? $"Neuer Ausgangsplan von außen: {bester}" : "Neuer Ausgangsplan passt nicht ins Modell – es geht mit dem bisherigen weiter.");
                if (gewechselt)
                {
                    e.Ausschnitt?.Invoke(new Ausschnittsmeldung(0, Ausschnittsergebnis.Ausgangsplan, bester));
                    groesse = e.Mannschaften;
                    ohneErfolg = 0;
                }
            }

            if (abbruch.IsCancellationRequested)
            {
                break;
            }

            schritte++;
            if (ohneErfolg >= geduld)
            {
                groesse = groesse >= hoechstens ? e.Mannschaften : groesse + 1;
                ohneErfolg = 0;
            }

            ohneErfolg++;
            (HashSet<int> frei, string ausschnitt) = MannschaftenFrei(zufall, groesse);
            for (int i = 0; i < ansetzung.Length; i++)
            {
                double v = Math.Round(aktuell[ansetzung[i]]);
                modell.ZeileGrenzen(zeile[i], frei.Contains(ansetzung[i]) ? 0 : v, frei.Contains(ansetzung[i]) ? 1 : v);
            }

            e.Ausschnitt?.Invoke(new Ausschnittsmeldung(groesse, Ausschnittsergebnis.Laeuft, bester));
            var schritt = Stopwatch.StartNew();
            double alt = Wert(ziel, aktuell);
            (_, _, double[]? werte, _) = Minimieren(modell, ziel, loeser, e.JeSchritt * groesse / e.Mannschaften, aktuell, Aufhoeren);
            double neu = werte is null ? alt : Wert(ziel, werte);
            string zeit = schritt.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            if (werte is null || neu > alt - 0.5)
            {
                Melden(string.Create(CultureInfo.InvariantCulture, $"{schritte}: {ausschnitt} ({frei.Count} Spalten frei, {zeit} s): {(werte is null ? "keine Lösung" : "nichts Besseres")}"));
                e.Ausschnitt?.Invoke(new Ausschnittsmeldung(groesse, Ausschnittsergebnis.NichtsBesseres, bester));
                continue;
            }

            // Die echte Bewertung entscheidet (auch über Kriterien, die das Modell nicht kennt).
            List<Spiel> kandidat = Plan(werte);
            Stufenwert w = Bewerten(kandidat);
            bool besser = e.Besser?.Invoke(w, bester) ?? Stufenvergleich.Besser(w, bester);
            Melden(string.Create(CultureInfo.InvariantCulture, $"{schritte}: {ausschnitt} ({frei.Count} Spalten frei, {zeit} s): Modell {alt:0} → {neu:0}, {w} – {(besser ? "angenommen" : "abgelehnt")}"));
            if (besser)
            {
                aktuell = werte;
                besterPlan = kandidat;
                bester = w;
                angenommen++;
                ohneErfolg = 0;

                // Gleich weitergeben (z. B. an den Automodus), nicht erst am Ende.
                e.Verbessert?.Invoke(kandidat, w);
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

    /// <summary>Ausschnitt: alle Spiele einiger Mannschaften auf allen ihren Terminen.</summary>
    private (HashSet<int> Frei, string Text) MannschaftenFrei(Random zufall, int anzahl)
    {
        int[] wahl = Enumerable.Range(0, d.N).OrderBy(_ => zufall.Next()).Take(Math.Min(anzahl, d.N)).ToArray();
        var frei = termine.Where(t => wahl.Contains(d.SpielHeim[t.Spiel]) || wahl.Contains(d.SpielGast[t.Spiel])).Select(t => t.Spalte).ToHashSet();
        frei.UnionWith(ohneTermin.Where(u => wahl.Contains(d.SpielHeim[u.Spiel]) || wahl.Contains(d.SpielGast[u.Spiel])).Select(u => u.Spalte));
        return (frei, "Mannschaften " + string.Join(", ", wahl.Select(t => d.Name[t])));
    }
}
