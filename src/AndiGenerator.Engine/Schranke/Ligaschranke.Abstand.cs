// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Abstand Heim/Auswärts und Setzliste genähert (nur für die Teilneuplanung, wenn es die genaue Form nicht gibt – siehe
/// <see cref="AbstandGenau"/> und <see cref="SetzlisteGenau"/>), dazu die 60-km-Regel und die Pausen der Spielverteilung.
/// </summary>
public sealed partial class Ligaschranke
{
    /// <summary>
    /// Wie <c>KernPlan.AbstandHeimAuswaerts</c>, genähert: Das Original zählt je Mannschaft, wie weit der unregelmäßigste
    /// Abstand zwischen Hin- und Rückspiel (in eigenen Spielen) vom mittleren abweicht. Das Modell misst stattdessen den
    /// Abstand zum gespiegelten Plan: Die Lage jedes Spiels in seiner Halbrunde (0 = erster, 1 = letzter Tag) mal der Zahl der
    /// Spiele je Halbrunde; je Paarung die Abweichung zwischen Hin- und Rückspiel, je Mannschaft die größte. Im gespiegelten
    /// Plan ist alles 0 – das Original zählt dort ebenfalls 0. Nur für Hin- und Rückrunde (auch nur Rückrunde) ohne
    /// Doppelrunde; sonst leer.
    /// </summary>
    private List<(int Spalte, double Faktor)> AbstandHeimAuswaerts()
    {
        if (d.Doppelrunde || d.Rundenplanung is not (Rundenplanung.Beide or Rundenplanung.NurRueckrunde) || d.ErstesEchtesDatum.Length < 2)
        {
            return [];
        }

        int spiele = d.N - 1 + (d.N % 2);
        double Lage(double datum, int runde) => Anteil(datum, runde) * spiele;

        var hoechste = Enumerable.Range(0, d.N).Select(_ => modell.Zahl(0, spiele)).ToArray();
        var jeSpiel = termine.ToLookup(t => t.Spiel);
        for (int h = 0; h < d.N; h++)
        {
            for (int g = h + 1; g < d.N; g++)
            {
                // Lage je Halbrunde: Ausdruck über die Termin-Spalten bzw. fest (Vorrunde bei „nur Rückrunde“).
                var lage = new List<(int Spalte, double Faktor)>[2] { [], [] };
                double[] fest = new double[2];
                foreach (int s in d.Paarung[(h * d.N) + g].Concat(d.Paarung[(g * d.N) + h]))
                {
                    foreach ((int spalte, _, int w) in jeSpiel[s])
                    {
                        int r = d.WunschRunde[w];
                        if (r < 2)
                        {
                            lage[r].Add((spalte, Lage(d.WunschDatum[w], r)));
                        }
                    }
                }

                FesterTermin[] vorrunde = feste.Where(f => (f.Heim == h && f.Gast == g) || (f.Heim == g && f.Gast == h)).ToArray();
                foreach ((double datum, int r) in vorrunde.Select(f => (f.Datum, d.RundeZuDatum(f.Datum))).Where(e => e.Item2 < 2))
                {
                    fest[r] += Lage(datum, r);
                }

                if ((lage[0].Count == 0 && vorrunde.Length == 0) || lage[1].Count == 0)
                {
                    continue;
                }

                // z ≥ |Lage Rückrunde − Lage Hinrunde| − 0,5 (gerundet wie im Original); je Mannschaft das größte z.
                int z = modell.Zahl(0, spiele);
                double konstant = fest[1] - fest[0];
                modell.Zeile([(z, 1.0), .. lage[1].Select(e => (e.Spalte, -e.Faktor)), .. lage[0].Select(e => (e.Spalte, e.Faktor))], konstant - 0.5, double.PositiveInfinity);
                modell.Zeile([(z, 1.0), .. lage[1].Select(e => (e.Spalte, e.Faktor)), .. lage[0].Select(e => (e.Spalte, -e.Faktor))], -konstant - 0.5, double.PositiveInfinity);
                modell.Zeile([(hoechste[h], 1.0), (z, -1.0)], 0, double.PositiveInfinity);
                modell.Zeile([(hoechste[g], 1.0), (z, -1.0)], 0, double.PositiveInfinity);
            }
        }

        return hoechste.Select(c => (c, 1.0)).ToList();
    }

    /// <summary>
    /// Wie <c>KernPlan.Setzliste</c>, genähert: Das Original zählt je Mannschaft die Spiele, die in ihrer Halbrunde zu spät
    /// liegen – das i-te Spiel (ab 0) gegen einen Gegner, der in der Setzliste a Plätze entfernt ist, verletzt sie, wenn
    /// i &gt; N − 1 − a (weit auseinander Gesetzte spielen früh gegeneinander, Nachbarn zuletzt). Das Modell schätzt i aus der
    /// Lage des Termins in seiner Halbrunde; jede Termin-Spalte kostet so viel, wie Mannschaften verletzt wären (0 oder 2).
    /// Ohne Setzliste leer.
    /// </summary>
    private List<(int Spalte, double Faktor)> Setzliste()
    {
        if (!d.HatSetzliste || d.ErstesEchtesDatum.Length == 0)
        {
            return [];
        }

        var ziel = new List<(int Spalte, double Faktor)>();
        foreach ((int spalte, int s, int w) in termine)
        {
            int r = d.WunschRunde[w];
            int heim = d.SetzlistenRang[d.SpielHeim[s]];
            int gast = d.SetzlistenRang[d.SpielGast[s]];
            if (r >= d.ErstesEchtesDatum.Length || heim < 0 || gast < 0)
            {
                continue;
            }

            double i = Anteil(d.WunschDatum[w], r) * Math.Max(0, d.N - 2);
            if (i > d.N - 1 - Math.Abs(heim - gast) + 0.5)
            {
                ziel.Add((spalte, 2.0));
            }
        }

        return ziel;
    }

    /// <summary>Lage eines Datums in seiner Runde: 0 am ersten, 1 am letzten echten Spieltag.</summary>
    private double Anteil(double datum, int runde)
    {
        double von = d.ErstesEchtesDatum[runde];
        double bis = d.LetztesEchtesDatum[runde];
        return bis > von ? Math.Clamp((datum - von) / (bis - von), 0, 1) : 0;
    }

    /// <summary>
    /// Wie <c>KernPlan.SechzigKilometer</c> (genau): ein Auswärtsspiel bei einem Gegner, gegen den unter der Woche nicht gespielt
    /// werden soll, an einem Werktag. Jede solche Termin-Spalte kostet 1.
    /// </summary>
    private List<(int Spalte, double Faktor)> SechzigKilometer()
    {
        if (!d.HatSechzigKilometer)
        {
            return [];
        }

        return termine
            .Where(e => d.KeinWochenspielGegen[(d.SpielGast[e.Spiel] * d.N) + d.SpielHeim[e.Spiel]] && !d.IstWochenendeFuer60Km(d.WunschDatum[e.Wunsch]))
            .Select(e => (e.Spalte, 1.0))
            .ToList();
    }

    /// <summary>
    /// Pausen der Spielverteilung wie <c>KernPlan.ZusatzZaehlen</c>, genau: eine Lücke zwischen zwei Spielen einer Mannschaft, in der
    /// im Mittel mindestens zwei Spieltage liegen (am Rand der Runde anderthalb). Grenzt ein gekoppeltes Paar an die Lücke (ihr
    /// erstes Spiel ist das zweite eines Paares bzw. ihr zweites Spiel das erste eines Paares), teilt das Original die Spieltage
    /// je Seite durch 1,5 – die Lücke muss dann drei bzw. viereinhalb Spieltage lang sein.
    /// <para>
    /// Je Mannschaft, Runde und möglichem Spieltag a eine Ja/Nein-Spalte p (Pause nach a) mit Zeilen für jeden möglichen nächsten
    /// Spieltag n (über „spielt am Tag“ je Tag): Spielt sie an a und an n und dazwischen nicht, ist p 1 – außer die Koppelpaare
    /// erlauben die Lücke (in [E2, E3): eines der beiden, ab E3 bis E4,5: beide; danach keines). Liegt bis E4,5 kein Spiel, ist
    /// p 1 (ebenso am Rundenende ab anderthalb bzw. 2,25 Spieltagen); zu Beginn der Runde ebenso mit anderthalb Spieltagen.
    /// </para>
    /// </summary>
    private List<(int Spalte, double Faktor)> Pausen()
    {
        var ziel = new List<(int Spalte, double Faktor)>();
        for (int r = 0; r < d.Runden.Length && r < d.ErstesEchtesDatum.Length; r++)
        {
            if (!d.RundeZuPlanen[r] || d.LetztesEchtesDatum[r] <= d.ErstesEchtesDatum[r])
            {
                continue;
            }

            // Ganze Tage wie in SollSpiele (das erste und letzte echte Datum tragen eine Uhrzeit).
            double rundeVon = DelphiDatum.Trunc(d.ErstesEchtesDatum[r]);
            double rundeBis = DelphiDatum.Trunc(d.LetztesEchtesDatum[r]) + 1;
            for (int t = 0; t < d.N; t++)
            {
                var spalten = jeMannschaft[t]
                    .Where(i => d.WunschRunde[termine[i].Wunsch] == r)
                    .Select(i => (Tag: d.WunschTag[termine[i].Wunsch], termine[i].Spalte))
                    .ToList();
                if (spalten.Count == 0)
                {
                    continue;
                }

                PausenDerMannschaft(ziel, r, t, spalten, rundeVon, rundeBis);
            }
        }

        return ziel;
    }

    /// <summary>Die Pausen einer Mannschaft in einer Runde (siehe <see cref="Pausen"/>).</summary>
    private void PausenDerMannschaft(List<(int Spalte, double Faktor)> ziel, int r, int t, List<(int Tag, int Spalte)> spalten, double rundeVon, double rundeBis)
    {
        List<(int Spalte, int Tag1, int Tag2)> paare = KoppelPaare(t);
        int[] tage = spalten.Select(e => e.Tag).Distinct().Order().ToArray();
        List<(int Spalte, double Faktor)> Beginnt(int n) => paare.Where(p => p.Tag1 == n).Select(p => (p.Spalte, 1.0)).ToList();
        List<(int Spalte, double Faktor)> Endet(int a) => paare.Where(p => p.Tag2 == a).Select(p => (p.Spalte, 1.0)).ToList();
        List<(int Spalte, double Faktor)> Zwischen(double von, double bis) => spalten.Where(e => e.Tag > von && e.Tag < bis).Select(e => (e.Spalte, 1.0)).ToList();

        // „Spielt am Tag“ q je Tag (eine Mannschaft hat je Tag viele mögliche Spiele, je Gegner eines; gespielt werden höchstens
        // zwei, bei Koppelterminen): 2q ≥ Σ Spiele des Tages und q ≤ Σ.
        var spielt = new Dictionary<int, int>();
        foreach (IGrouping<int, int> tag in spalten.GroupBy(e => e.Tag, e => e.Spalte))
        {
            int q = modell.Spalte(0);
            spielt[tag.Key] = q;
            modell.Zeile([(q, 2.0), .. tag.Select(c => (c, -1.0))], 0, double.PositiveInfinity);
            modell.Zeile([(q, 1.0), .. tag.Select(c => (c, -1.0))], double.NegativeInfinity, 0);
        }

        // Zu Beginn der Runde: p ≥ x(n) − Spiele davor − „n beginnt ein Paar“ für jeden ersten Spieltag n ab anderthalb
        // Spieltagen; ab 2,25 Spieltagen ohne Spiel in jedem Fall.
        double anfang = Fensterende(r, rundeVon, 1.5, rundeBis);
        double anfangGekoppelt = Fensterende(r, rundeVon, 2.25, rundeBis);
        if (anfang <= rundeBis)
        {
            int p = modell.Spalte(0);
            ziel.Add((p, 1));
            foreach (int n in tage.Where(n => n >= anfang && n < anfangGekoppelt))
            {
                modell.Zeile([(p, 1.0), (spielt[n], -1.0), .. Zwischen(double.NegativeInfinity, n), .. Beginnt(n)], 0, double.PositiveInfinity);
            }

            if (anfangGekoppelt <= rundeBis)
            {
                modell.Zeile([(p, 1.0), .. Zwischen(double.NegativeInfinity, anfangGekoppelt)], 1, double.PositiveInfinity);
            }
        }

        foreach (int tag in tage)
        {
            double e2 = Fensterende(r, tag, 2, rundeBis);
            double e3 = Fensterende(r, tag, 3, rundeBis);
            double e45 = Fensterende(r, tag, 4.5, rundeBis);
            List<(int Spalte, double Faktor)> endet = Endet(tag);
            int p = -1;
            int Pause()
            {
                if (p < 0)
                {
                    p = modell.Spalte(0);
                    ziel.Add((p, 1));
                }

                return p;
            }

            // Nächster Spieltag n in [E2, E3): Pause, außer a endet ein Paar oder n beginnt eines.
            foreach (int n in tage.Where(n => n >= e2 && n < e3))
            {
                modell.Zeile([(Pause(), 1.0), (spielt[tag], -1.0), (spielt[n], -1.0), .. Zwischen(tag, n), .. endet, .. Beginnt(n)], -1, double.PositiveInfinity);
            }

            // Nächster Spieltag n in [E3, E4,5): Pause, außer beides (w ≤ beide).
            foreach (int n in tage.Where(n => n >= e3 && n < e45))
            {
                List<(int Spalte, double Faktor)> beginnt = Beginnt(n);
                var erlaubt = new List<(int Spalte, double Faktor)>();
                if (endet.Count > 0 && beginnt.Count > 0)
                {
                    int w = modell.Spalte(0);
                    modell.Zeile([(w, 1.0), .. endet.Select(e => (e.Spalte, -1.0))], double.NegativeInfinity, 0);
                    modell.Zeile([(w, 1.0), .. beginnt.Select(e => (e.Spalte, -1.0))], double.NegativeInfinity, 0);
                    erlaubt.Add((w, 1.0));
                }

                modell.Zeile([(Pause(), 1.0), (spielt[tag], -1.0), (spielt[n], -1.0), .. Zwischen(tag, n), .. erlaubt], -1, double.PositiveInfinity);
            }

            // Kein Spiel bis E4,5 (bzw. bis zum Rundenende ab anderthalb Spieltagen, mit Paar davor ab 2,25): Pause.
            List<(int Spalte, double Faktor)> rest;
            double soll = d.SollSpiele(r, tag, rundeBis);
            if (e45 <= rundeBis)
            {
                rest = [];
                e45 = Math.Min(e45, rundeBis);
            }
            else if (soll >= 2.25)
            {
                rest = [];
                e45 = rundeBis;
            }
            else if (soll >= 1.5)
            {
                rest = endet;
                e45 = rundeBis;
            }
            else
            {
                continue;
            }

            modell.Zeile([(Pause(), 1.0), (spielt[tag], -1.0), .. Zwischen(tag, e45), .. rest], 0, double.PositiveInfinity);
        }
    }

    /// <summary>Der erste Tag ab <paramref name="von"/>, bis zu dem im Mittel <paramref name="spieltage"/> Spieltage liegen.</summary>
    private double Fensterende(int runde, double von, double spieltage, double rundeBis)
    {
        double bis = von + 1;
        while (bis <= rundeBis && d.SollSpiele(runde, von, bis) < spieltage)
        {
            bis++;
        }

        return bis;
    }
}
