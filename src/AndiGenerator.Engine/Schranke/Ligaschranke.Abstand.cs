// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Kern;

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Abstand Heim/Auswärts und Setzliste genähert (nur für die Teilneuplanung), dazu die 60-km-Regel und die Pausen der
/// Spielverteilung.
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
    /// Pausen der Spielverteilung wie <c>KernPlan.ZusatzZaehlen</c>: eine Lücke zwischen zwei Spielen einer Mannschaft, in der im
    /// Mittel mindestens zwei Spieltage liegen (am Rand der Runde anderthalb). Je Mannschaft und möglichem Spieltag a: Spielt
    /// sie an a und bis zum Ende des Fensters, in dem zwei Spieltage liegen, nicht mehr, zählt ein Verstoß; ebenso, wenn sie
    /// zu Beginn der Runde anderthalb Spieltage lang nicht spielt. Genähert: Koppeltermine verkürzen die Lücke hier nicht.
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

            double rundeVon = d.ErstesEchtesDatum[r];
            double rundeBis = d.LetztesEchtesDatum[r] + 1;
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

                // Zu Beginn der Runde: anderthalb Spieltage ohne Spiel.
                double anfang = Fensterende(r, rundeVon, 1.5, rundeBis);
                if (anfang <= rundeBis)
                {
                    Pause(ziel, [], spalten.Where(e => e.Tag < anfang).Select(e => e.Spalte));
                }

                foreach (int tag in spalten.Select(e => e.Tag).Distinct())
                {
                    double ende = Fensterende(r, tag, 2, rundeBis);
                    if (ende > rundeBis)
                    {
                        // Bis zum Rundenende: dort reichen anderthalb Spieltage.
                        if (d.SollSpiele(r, tag, rundeBis) < 1.5)
                        {
                            continue;
                        }

                        ende = rundeBis;
                    }

                    Pause(ziel, spalten.Where(e => e.Tag == tag).Select(e => e.Spalte), spalten.Where(e => e.Tag > tag && e.Tag < ende).Select(e => e.Spalte));
                }
            }
        }

        return ziel;
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

    /// <summary>
    /// p ≥ (Spiel an diesem Tag, sonst 1) − Spiele im Fenster danach. Je Termin des Tages eine Zeile: Mit zwei Spielen am selben
    /// Tag (Koppeltermin) wäre die Summe 2 und das Modell für p ≤ 1 unlösbar.
    /// </summary>
    private void Pause(List<(int Spalte, double Faktor)> ziel, IEnumerable<int> amTag, IEnumerable<int> danach)
    {
        int p = modell.Spalte(0);
        List<int> tag = amTag.ToList();
        List<(int Spalte, double Faktor)> spaeter = danach.Select(c => (c, 1.0)).ToList();
        if (tag.Count == 0)
        {
            modell.Zeile([(p, 1.0), .. spaeter], 1, double.PositiveInfinity);
        }

        foreach (int c in tag)
        {
            modell.Zeile([(p, 1.0), (c, -1.0), .. spaeter], 0, double.PositiveInfinity);
        }

        ziel.Add((p, 1));
    }
}
