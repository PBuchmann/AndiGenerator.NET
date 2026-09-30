// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Kostenarten je Mannschaft auf Arrays (Original <c>TMannschaft.CalculateKosten*</c>). Rechenweg und Reihenfolge der
/// Gleitkommaoperationen folgen dem Referenzmodell genau, damit die Ergebnisse bitgleich sind.
/// </summary>
internal sealed partial class KernPlan
{
    /// <summary>
    /// Original <c>CalculateKostenForType</c>: schreibt Anzahl, Bezugsgröße und Kosten der Mannschaft.
    /// Rechnet immer neu und verwirft den Cache dieser Kostenart.
    /// </summary>
    public double KostenFuerArt(int t, MannschaftsKostenart art)
    {
        artBerechnet[(int)art] = false;
        return KostenBerechnen(t, art);
    }

    /// <summary>Original <c>CalculateMaxAbweichung</c> über die ersten <paramref name="anzahl"/> Werte.</summary>
    private static (int Echt, double Korrigiert, double Mittel) MaxAbweichung(int[] werte, int anzahl, double faktorKleiner, double faktorGroesser)
    {
        int echt = 0;
        double korrigiert = 0;
        if (anzahl <= 1)
        {
            return (echt, korrigiert, 1);
        }

        double mittel = 0;
        for (int i = 0; i < anzahl; i++)
        {
            mittel += werte[i];
        }

        mittel /= anzahl * 1.0;
        for (int i = 0; i < anzahl; i++)
        {
            int wert = werte[i];
            double faktor = wert < mittel ? faktorKleiner : faktorGroesser;
            korrigiert = Math.Max(faktor * Math.Abs(wert - mittel), korrigiert);
            echt = Math.Max(DelphiDatum.Trunc(Math.Abs(wert - mittel) + 0.5), echt);
        }

        return (echt, korrigiert, mittel);
    }

    /// <summary>Original <c>CalculateStandardAbweichung</c> über die ersten <paramref name="anzahl"/> Werte.</summary>
    private static double Standardabweichung(double[] werte, int anzahl)
    {
        if (anzahl <= 1)
        {
            return 0;
        }

        double mittel = 0;
        for (int i = 0; i < anzahl; i++)
        {
            mittel += werte[i];
        }

        mittel /= anzahl * 1.0;
        double summe = 0;
        for (int i = 0; i < anzahl; i++)
        {
            double d = werte[i] - mittel;
            summe += d * d;
        }

        return Math.Sqrt(summe / (anzahl * 1.0));
    }

    /// <summary>Rechnet eine Kostenart einer Mannschaft (ohne Cache-Verwaltung).</summary>
    private double KostenBerechnen(int t, MannschaftsKostenart art)
    {
        int k = (t * KernDefinition.AnzahlArten) + (int)art;
        KostenAnzahl[k] = 0;
        KostenAlle[k] = -1;
        KostenWert[k] = 0;
        double faktor = D.Faktor[k];
        switch (art)
        {
            case MannschaftsKostenart.Hallenbelegung: Hallenbelegung(t, k, faktor); break;
            case MannschaftsKostenart.ParalleleSpiele: ParalleleSpieleKosten(t, k, faktor); break;
            case MannschaftsKostenart.GleicheHeimtermine: GleicheHeimtermine(t, k, faktor); break;
            case MannschaftsKostenart.Sperrtermine: SperrtermineKosten(t, k, faktor); break;
            case MannschaftsKostenart.Ausweichtermine: AusweichtermineKosten(t, k, faktor); break;
            case MannschaftsKostenart.SechzigKilometerRegel: SechzigKilometer(t, k, faktor); break;
            case MannschaftsKostenart.DreiTageAbstand: EngeTermine(t, k, faktor); break;
            case MannschaftsKostenart.ZweiSpieleProWoche: ZweiSpieleDieWoche(t, k, faktor); break;
            case MannschaftsKostenart.Spielverteilung: Spielverteilung(t, k, faktor); break;
            case MannschaftsKostenart.Heimkoppel: Koppeltermine(t, k, faktor); break;
            case MannschaftsKostenart.Auswaertskoppel: AuswaertsKoppeltermine(t, k, faktor); break;
            case MannschaftsKostenart.UngleichHeimAuswaerts: ZahlHeimspiele(t, k, faktor); break;
            case MannschaftsKostenart.WechselHeimAuswaerts: WechselHeimAuswaerts(t, k, faktor); break;
            case MannschaftsKostenart.AbstandHeimAuswaerts: AbstandHeimAuswaerts(t, k, faktor); break;
            case MannschaftsKostenart.Setzliste: Setzliste(t, k, faktor); break;
            default: Pflichtspieltage(t, k, faktor); break;
        }

        return KostenWert[k];
    }

    private int Gegner(int t, int s) => Heim[s] == t ? Gast[s] : Heim[s];

    private bool Aktiv(int s) => Datum[s] != 0.0 && InPlanung[s];

    private void AbstandHeimAuswaerts(int t, int k, double faktor)
    {
        if (D.Rundenplanung is Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde or Rundenplanung.Corona && !D.Doppelrunde)
        {
            return;
        }

        if (D.N > MaxMannschaft)
        {
            throw new InvalidOperationException($"Der Plan enthält zu viele Mannschaften. Für maximal {MaxMannschaft} kann der Plan generiert werden");
        }

        Array.Clear(positionenAnzahl);
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            if (Datum[liste[i]] == 0.0)
            {
                continue;
            }

            int gegner = Gegner(t, liste[i]);
            if (positionenAnzahl[gegner] >= MaxRunde - 1)
            {
                continue;
            }

            positionen[(gegner * MaxRunde) + positionenAnzahl[gegner]] = i;
            positionenAnzahl[gegner]++;
        }

        int anzahl = 0;
        for (int j = 0; j < D.N; j++)
        {
            if (j == t)
            {
                continue;
            }

            for (int i = 0; i < positionenAnzahl[j] - 1; i++)
            {
                werteGanz[anzahl++] = positionen[(j * MaxRunde) + i + 1] - positionen[(j * MaxRunde) + i];
            }
        }

        (int echt, double wert, double mittel) = MaxAbweichung(werteGanz, anzahl, 2, 1);
        KostenAnzahl[k] = echt;
        wert /= mittel;
        KostenWert[k] = Math.Pow(wert * 6, 6) / 5000.0 * faktor;
    }

    private void Setzliste(int t, int k, double faktor)
    {
        if (!D.HatSetzliste)
        {
            return;
        }

        int eigenerRang = D.SetzlistenRang[t];
        int ziel = D.N - 1;
        int runde = -1;
        double wert = 0;
        int anzahl = 0;
        double rundenFaktor = 1.0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (Datum[s] == 0.0)
            {
                continue;
            }

            int spielRunde = Runde[s];
            if (runde != spielRunde)
            {
                ziel = D.N - 1;
                runde = spielRunde;
                if (runde == D.Runden.Length - 1)
                {
                    rundenFaktor = 4.0;
                }
            }
            else
            {
                ziel--;
            }

            int gegnerRang = D.SetzlistenRang[Gegner(t, s)];
            int abstand = gegnerRang < 0 || eigenerRang < 0 ? 0 : Math.Abs(eigenerRang - gegnerRang);
            int diff = abstand - ziel;
            if (diff > 0 && ziel > 0)
            {
                wert += rundenFaktor * (diff / Math.Pow(ziel, 1.25));
                anzahl++;
            }
        }

        KostenAnzahl[k] = anzahl;
        KostenWert[k] = Math.Pow(wert / 8.0, 3) * faktor;
    }

    private void Pflichtspieltage(int t, int k, double faktor)
    {
        int alle = 0;
        int[] liste = Spiele[t];
        foreach (RefPflichtzeit zeit in D.Pflichtzeiten)
        {
            if (!D.IstInZuPlanenderRunde(zeit.Von))
            {
                continue;
            }

            alle += zeit.AnzahlSpiele;
            int anzahl = 0;
            for (int i = ErsterIndexAb(t, zeit.Von); i < SpieleAnzahl[t]; i++)
            {
                int s = liste[i];
                if (Datum[s] == 0.0)
                {
                    continue;
                }

                int tag = Tag[s];
                if (tag >= zeit.Von && tag <= zeit.Bis)
                {
                    anzahl++;
                }

                if (tag > zeit.Bis)
                {
                    break;
                }
            }

            if (anzahl < zeit.AnzahlSpiele)
            {
                KostenAnzahl[k] += zeit.AnzahlSpiele - anzahl;
            }
        }

        double wert = KostenAnzahl[k] * 1.0;
        KostenWert[k] = Math.Pow(wert, 2) * 1000 * faktor;
        KostenAlle[k] = alle;
    }

    /// <summary>Original <c>TGameList.getFirstGameRefIndexByDate</c>.</summary>
    private int ErsterIndexAb(int t, double datum)
    {
        int[] liste = Spiele[t];
        int l = 0;
        int h = SpieleAnzahl[t] - 1;
        while (l <= h)
        {
            int i = (l + h) >> 1;
            int c = Datum[liste[i]].CompareTo(datum);
            if (c < 0)
            {
                l = i + 1;
            }
            else
            {
                h = i - 1;
                if (c == 0)
                {
                    l = i;
                    break;
                }
            }
        }

        return l;
    }

    private void WechselHeimAuswaerts(int t, int k, double faktor)
    {
        int gleich = 0;
        bool letztesHeim = false;
        double wert = 0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            if (!Aktiv(liste[i]))
            {
                continue;
            }

            bool heim = Heim[liste[i]] == t;
            if (i != 0)
            {
                if (heim != letztesHeim)
                {
                    gleich = 0;
                }
                else if (liste[i - 1] != Gekoppelt(t, i))
                {
                    gleich++;
                    KostenAnzahl[k]++;
                    wert += Math.Pow(gleich, 4);
                }
            }

            letztesHeim = heim;
        }

        KostenWert[k] = (Math.Pow(wert, 2) / 30.0) * faktor;
    }

    private void ZahlHeimspiele(int t, int k, double faktor)
    {
        bool rundenIgnorieren = D.Rundenplanung == Rundenplanung.Corona || (D.Rundenplanung == Rundenplanung.Halbrunde && !D.Doppelrunde);
        int rundenAnzahl = rundenIgnorieren ? 1 : D.Runden.Length;
        Array.Clear(jeRunde);
        int[] liste = Spiele[t];
        int anzahlSpiele = SpieleAnzahl[t];

        int heimrechtFehler = 0;
        if (D.HatHeimrechte[t])
        {
            for (int i = 0; i < anzahlSpiele; i++)
            {
                int s = liste[i];
                if (Datum[s] == 0.0)
                {
                    continue;
                }

                HeimrechtWert recht = D.HeimrechtGegen[(t * D.N) + Gegner(t, s)];
                if (recht == HeimrechtWert.Keins)
                {
                    continue;
                }

                int runde = rundenIgnorieren ? 0 : Runde[s];
                bool zielHeim = runde % 2 == 0;
                if (recht == HeimrechtWert.Runde2)
                {
                    zielHeim = !zielHeim;
                }

                if ((Heim[s] == t) != zielHeim)
                {
                    heimrechtFehler++;
                }
            }
        }

        for (int i = 0; i < anzahlSpiele; i++)
        {
            int s = liste[i];
            if (Datum[s] == 0.0 || Heim[s] != t)
            {
                continue;
            }

            int runde = rundenIgnorieren ? 0 : Runde[s];
            if (runde >= 0)
            {
                jeRunde[runde]++;
            }
        }

        double ziel;
        if (rundenIgnorieren)
        {
            int notwendig = 0;
            for (int i = 0; i < anzahlSpiele; i++)
            {
                if (!NichtNotwendig[liste[i]])
                {
                    notwendig++;
                }
            }

            ziel = notwendig * 0.5 / 1;
        }
        else
        {
            ziel = anzahlSpiele * 0.5 / D.Runden.Length;
            if (D.Rundenplanung == Rundenplanung.Halbrunde && D.Doppelrunde)
            {
                ziel = anzahlSpiele * 0.25 / D.Runden.Length;
            }
        }

        int anzahl = 0;
        if (rundenIgnorieren)
        {
            anzahl += DelphiDatum.Trunc(Math.Abs(jeRunde[0] - ziel));
        }
        else
        {
            for (int i = 0; i < rundenAnzahl; i++)
            {
                if (!D.RundeZuPlanen[i])
                {
                    continue;
                }

                if (D.HeimspieleRundeEins[t] >= 0)
                {
                    ziel = i % 2 == 0 ? D.HeimspieleRundeEins[t] : D.N - D.HeimspieleRundeEins[t] - 1;
                }

                anzahl += DelphiDatum.Trunc(Math.Abs(jeRunde[i] - ziel));
            }
        }

        double wert = anzahl + (heimrechtFehler * 2);
        KostenWert[k] = Math.Pow(wert, 2) * 300 * faktor;
        KostenAnzahl[k] = anzahl + heimrechtFehler;
    }

    private void Koppeltermine(int t, int k, double faktor)
    {
        if (!D.HatKoppeltermine)
        {
            return;
        }

        const double niedrig = 0.25;
        const int n = MaxRunde + 1;
        Span<int> hoch = stackalloc int[n];
        Span<int> weich = stackalloc int[n];
        Span<int> erreichtHoch = stackalloc int[n];
        Span<int> erreichtWeich = stackalloc int[n];
        Span<double> optimum = stackalloc double[n];
        Span<double> erreicht = stackalloc double[n];
        hoch.Clear();
        weich.Clear();
        erreichtHoch.Clear();
        erreichtWeich.Clear();
        optimum.Clear();
        erreicht.Clear();

        int alle = 0;
        for (int w = D.WunschStart[t]; w < D.WunschStart[t + 1]; w++)
        {
            Wunschterminoptionen optionen = D.WunschOptionen[w];
            if (!Terminoptionen.IstKoppel(optionen) || !D.WunschInPlanung[w])
            {
                continue;
            }

            alle++;
            int runde = D.WunschRunde[w];
            if (Terminoptionen.IstKoppelHart(optionen))
            {
                hoch[runde]++;
            }

            if (Terminoptionen.IstKoppelWeich(optionen))
            {
                weich[runde]++;
            }
        }

        if (alle <= 0)
        {
            return;
        }

        int maxJeRunde = (D.N - 1) / 4;
        for (int i = 0; i < D.Runden.Length; i++)
        {
            hoch[i] /= 2;
            weich[i] /= 2;
            if (hoch[i] >= maxJeRunde)
            {
                hoch[i] = maxJeRunde;
                weich[i] = maxJeRunde;
            }

            if (weich[i] + hoch[i] > maxJeRunde)
            {
                weich[i] = maxJeRunde - hoch[i];
            }

            optimum[i] = hoch[i] + (weich[i] * niedrig);
        }

        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (!Aktiv(s))
            {
                continue;
            }

            Wunschterminoptionen koppel = KoppelOptionen(t, s);
            if (Terminoptionen.Hat(koppel, Wunschterminoptionen.DoppelMoeglich) || Terminoptionen.Hat(koppel, Wunschterminoptionen.KoppelMoeglich))
            {
                continue;
            }

            if (Terminoptionen.IstKoppel(koppel) && GekoppeltDanach(t, i) >= 0)
            {
                int runde = Runde[s];
                if (Terminoptionen.Hat(koppel, Wunschterminoptionen.KoppelHart) || Terminoptionen.Hat(koppel, Wunschterminoptionen.DoppelHart))
                {
                    erreicht[runde] += 1.0;
                    erreichtHoch[runde]++;
                }
                else
                {
                    erreicht[runde] += niedrig;
                    erreichtWeich[runde]++;
                }
            }
        }

        double wert = 0;
        int anzahl = 0;
        int gesamt = 0;
        for (int i = 0; i < D.Runden.Length; i++)
        {
            if (optimum[i] > erreicht[i])
            {
                wert += optimum[i] - erreicht[i];
            }

            anzahl += erreichtHoch[i] + erreichtWeich[i];
            gesamt += hoch[i] + weich[i];
        }

        KostenAnzahl[k] = gesamt - anzahl;
        KostenAlle[k] = gesamt;
        wert *= 10.0;
        KostenWert[k] = Math.Pow(wert, 2) * faktor;
    }

    private void AuswaertsKoppeltermine(int t, int k, double faktor)
    {
        KostenAlle[k] = 0;
        (int A, int B, AuswaertsKoppelTyp Art)[] koppeln = D.Auswaertskoppeln[t];
        if (!D.HatAuswaertsKoppeltermine || koppeln.Length == 0)
        {
            return;
        }

        Array.Clear(zaehler);
        int zielwert = D.Runden.Length / 2;
        foreach ((int a, int b, AuswaertsKoppelTyp _) in koppeln)
        {
            if (a >= 0 && b >= 0)
            {
                zaehler[a] = zielwert;
                zaehler[b] = zielwert;
            }
        }

        int wunschAnzahl = 0;
        for (int i = 0; i < zaehler.Length; i++)
        {
            if (zaehler[i] > 0)
            {
                wunschAnzahl++;
            }
        }

        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            if (Gast[liste[i]] != t)
            {
                continue;
            }

            int zweites = GekoppeltDanach(t, i);
            if (zweites >= 0)
            {
                zaehler[Heim[liste[i]]]--;
                zaehler[Heim[zweites]]--;
            }
        }

        for (int i = 0; i < zaehler.Length; i++)
        {
            if (zaehler[i] > 0)
            {
                KostenAnzahl[k] += zaehler[i];
            }
        }

        double wunschFaktor = Math.Max(0, Math.Min(6, wunschAnzahl - 2)) / 6.0;
        wunschFaktor = 1.0 - (0.75 * wunschFaktor);
        double wert = KostenAnzahl[k] * 5.0;
        KostenWert[k] = Math.Pow(wert, 2) * 4.0 * faktor * wunschFaktor;
        KostenAnzahl[k] /= 2;
        KostenAlle[k] = wunschAnzahl / 2;
    }

    private void ZweiSpieleDieWoche(int t, int k, double faktor)
    {
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t] - 1; i++)
        {
            if (!Aktiv(liste[i]))
            {
                continue;
            }

            if (DelphiDatum.InterneWochennummer(Datum[liste[i + 1]]) == DelphiDatum.InterneWochennummer(Datum[liste[i]])
                && liste[i + 1] != Gekoppelt(t, i))
            {
                KostenAnzahl[k]++;
            }
        }

        double wert = KostenAnzahl[k] * 1.0;
        KostenWert[k] = Math.Pow(wert, 2) * 400 * faktor;
    }

    private void EngeTermine(int t, int k, double faktor)
    {
        const double fehlerPotenz = 4.3;
        double wert = 0.0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t] - 1; i++)
        {
            if (!Aktiv(liste[i]))
            {
                continue;
            }

            int abstand = Tag[liste[i + 1]] - Tag[liste[i]];
            if (abstand is <= 3 and >= 0 && liste[i + 1] != Gekoppelt(t, i))
            {
                KostenAnzahl[k]++;
                wert += Math.Pow(4.0 / (abstand + 1), fehlerPotenz);
            }
        }

        KostenWert[k] = Math.Pow(wert, 2) * faktor;
    }

    private void Spielverteilung(int t, int k, double faktor)
    {
        int anzahl = 0;
        if (SpieleAnzahl[t] > 0)
        {
            // Koppelungen einmal je Mannschaft ermitteln statt in jeder Runde erneut.
            for (int i = 0; i <= SpieleAnzahl[t]; i++)
            {
                koppelDanach[i] = GekoppeltDanach(t, i) >= 0;
            }

            for (int runde = 0; runde < D.Runden.Length; runde++)
            {
                if (D.RundeZuPlanen[runde])
                {
                    anzahl = SpielverteilungRunde(t, runde, anzahl);
                }
            }
        }

        double wert = 0.0;
        if (anzahl > 0)
        {
            wert = Standardabweichung(werteKomma, anzahl);
        }

        wert = Math.Pow(wert * 5, 5) / 10.0 * faktor;
        wert = wert / SpieleAnzahl[t] * 20;
        KostenAnzahl[k] = -1;
        KostenWert[k] = wert;
    }

    private int SpielverteilungRunde(int t, int runde, int anzahl)
    {
        int[] liste = Spiele[t];
        int spiele = SpieleAnzahl[t];
        for (int i = -1; i < spiele; i++)
        {
            bool erstes = false;
            bool letztes = false;
            double ende;
            double start;

            if (i < 0 || runde > Runde[liste[i]])
            {
                if (i + 1 >= spiele || runde != Runde[liste[i + 1]])
                {
                    continue;
                }

                erstes = true;
                start = D.ErstesEchtesDatum[runde];
                ende = Datum[liste[i + 1]];
            }
            else if (i >= spiele - 1 || runde < Runde[liste[i + 1]])
            {
                if (runde != Runde[liste[i]])
                {
                    continue;
                }

                letztes = true;
                ende = D.LetztesEchtesDatum[runde] + 1;
                start = Datum[liste[i]];
            }
            else
            {
                start = Datum[liste[i]];
                ende = Datum[liste[i + 1]];
            }

            if (ende > 0 && start > 0)
            {
                double soll = D.SollSpiele(erstes ? runde : Runde[liste[i]], start, ende);
                if (!letztes && koppelDanach[i + 1])
                {
                    soll /= 1.5;
                }

                if (!erstes && i > 0 && koppelDanach[i - 1])
                {
                    soll /= 1.5;
                }

                if (erstes || letztes)
                {
                    soll += 0.5;
                }

                werteKomma[anzahl++] = soll;
            }
        }

        return anzahl;
    }

    private void GleicheHeimtermine(int t, int k, double faktor)
    {
        int[] teams = D.GleicheHeimtageTeams[t];
        if (teams.Length == 0)
        {
            return;
        }

        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (Heim[s] == t && Aktiv(s))
            {
                KostenAnzahl[k] += FehlendeGleichzeitigeHeimspiele(t, teams, Tag[s]);
            }
        }

        double wert = KostenAnzahl[k];
        KostenWert[k] = Math.Pow(wert, 3) * 10 * faktor;
    }

    /// <summary>Original <c>GetMissingParallelHomeGames</c>.</summary>
    private int FehlendeGleichzeitigeHeimspiele(int t, int[] teams, int tag)
    {
        if (!D.Nachbarspiele[t].TryGetValue(tag, out Nachbarspiel[]? liste))
        {
            return teams.Length;
        }

        int fehlend = 0;
        foreach (int team in teams)
        {
            bool gefunden = false;
            foreach (Nachbarspiel spiel in liste)
            {
                if (spiel.IstHeimspiel && spiel.ParalleleHeimspiele && spiel.Tag == tag && spiel.Schluessel == team)
                {
                    gefunden = true;
                    break;
                }
            }

            if (!gefunden)
            {
                fehlend++;
            }
        }

        return fehlend;
    }

    private void SperrtermineKosten(int t, int k, double faktor)
    {
        double[] sperrtage = D.Sperrtage[t];
        KostenAlle[k] = sperrtage.Length;
        if (sperrtage.Length == 0)
        {
            return;
        }

        int verletzt = 0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            double tag = Tag[liste[i]];
            if (D.IstInZuPlanenderRunde(tag) && Array.BinarySearch(sperrtage, tag) >= 0)
            {
                verletzt++;
            }
        }

        if (verletzt == 0)
        {
            return;
        }

        KostenAnzahl[k] = verletzt;
        double wert = verletzt * 1.0 / sperrtage.Length * 20.0;
        wert *= D.AnzahlWunschtage * 1.0 / 50.0;
        wert = Math.Pow(wert, 3) * 200;
        KostenWert[k] = wert * faktor;
    }

    private void AusweichtermineKosten(int t, int k, double faktor)
    {
        if (!D.HatAusweichtermine)
        {
            return;
        }

        double[] ausweichtage = D.Ausweichtage[t];
        KostenAlle[k] = ausweichtage.Length;
        if (ausweichtage.Length > 0)
        {
            int[] liste = Spiele[t];
            for (int i = 0; i < SpieleAnzahl[t]; i++)
            {
                int s = liste[i];
                if (Heim[s] == t && InPlanung[s] && Array.BinarySearch(ausweichtage, (double)Tag[s]) >= 0)
                {
                    KostenAnzahl[k]++;
                }
            }
        }

        double wert = KostenAnzahl[k] * 1.0;
        KostenWert[k] = Math.Pow(wert, 2) * 10 * faktor;
    }

    private void Hallenbelegung(int t, int k, double faktor)
    {
        if (D.Nachbarspiele[t].Count == 0 && D.Vereinsteams[t].Length == 0)
        {
            return;
        }

        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (MaxHeim[s] > 0 && Heim[s] == t && Aktiv(s))
            {
                int belegung = Hallenbelegung(t, Datum[s], Tag[s], Lokal(s));
                if (belegung >= MaxHeim[s])
                {
                    KostenAnzahl[k] += belegung - MaxHeim[s] + 1;
                }
            }
        }

        double wert = KostenAnzahl[k] * 10.0;
        KostenWert[k] = Math.Pow(wert, 2) * 500 * faktor;
    }

    /// <summary>Original <c>GetHallenBelegung</c>.</summary>
    private int Hallenbelegung(int t, double datum, int tag, int lokal)
    {
        int anzahl = 0;
        double fenster = DelphiDatum.EncodeTime(1, 29);
        if (D.Nachbarspiele[t].TryGetValue(tag, out Nachbarspiel[]? nachbarn))
        {
            foreach (Nachbarspiel spiel in nachbarn)
            {
                if (spiel.IstHeimspiel && Math.Abs(spiel.Datum - datum) <= fenster && lokal == spiel.Lokal)
                {
                    anzahl++;
                }
            }
        }

        foreach (int verein in D.Vereinsteams[t])
        {
            int[] liste = Spiele[verein];
            for (int i = 0; i < SpieleAnzahl[verein]; i++)
            {
                int s = liste[i];
                if (Heim[s] == verein && Gast[s] != t && Math.Abs(Datum[s] - datum) <= fenster && Lokal(s) == lokal)
                {
                    anzahl++;
                }
            }
        }

        return anzahl;
    }

    private void ParalleleSpieleKosten(int t, int k, double faktor)
    {
        if (D.Nachbarspiele[t].Count == 0 && D.Vereinsteams[t].Length == 0)
        {
            return;
        }

        double wert = 0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (!Aktiv(s))
            {
                continue;
            }

            int anzahl = 0;
            ParalleleSpiele(t, Datum[s], Tag[s], D.Nummer[t], Suchrichtung.Beide, ref anzahl);
            if (anzahl > 0)
            {
                KostenAnzahl[k] += anzahl;
                wert += Math.Pow(anzahl, 3);
            }
        }

        KostenWert[k] = Math.Pow(wert, 1.5) * 20.0 * faktor;
    }

    /// <summary>Original <c>GetParallelGames</c> (rekursiv über benachbarte Mannschaftsnummern).</summary>
    private void ParalleleSpiele(int t, double datum, int tag, int teamNummer, Suchrichtung richtung, ref int anzahl)
    {
        double fenster = DelphiDatum.EncodeTime(3, 59);
        if (D.Nachbarspiele[t].TryGetValue(tag, out Nachbarspiel[]? nachbarn))
        {
            foreach (Nachbarspiel spiel in nachbarn)
            {
                bool pruefen;
                if (richtung == Suchrichtung.Beide)
                {
                    pruefen = spiel.KeineParallelenSpiele;
                }
                else
                {
                    int diff = teamNummer - spiel.Nummer;
                    pruefen = spiel.GleichesGeschlecht
                        && ((diff == -1 && richtung == Suchrichtung.Tiefer) || (diff == 1 && richtung == Suchrichtung.Hoeher));
                }

                if (pruefen && Math.Abs(spiel.Datum - datum) <= fenster)
                {
                    anzahl++;
                    WeiterSuchen(t, datum, tag, spiel.Nummer, richtung, ref anzahl);
                }
            }
        }

        foreach (int verein in D.Vereinsteams[t])
        {
            int diff = teamNummer - D.Nummer[verein];
            bool passend = (diff == -1 && richtung is Suchrichtung.Beide or Suchrichtung.Tiefer)
                || (diff == 1 && richtung is Suchrichtung.Beide or Suchrichtung.Hoeher);
            if (!passend)
            {
                continue;
            }

            int[] liste = Spiele[verein];
            for (int i = 0; i < SpieleAnzahl[verein]; i++)
            {
                int s = liste[i];
                if (Heim[s] != t && Gast[s] != t && Math.Abs(Datum[s] - datum) <= fenster)
                {
                    anzahl++;
                    WeiterSuchen(t, datum, tag, D.Nummer[verein], richtung, ref anzahl);
                }
            }
        }
    }

    private void WeiterSuchen(int t, double datum, int tag, int nummer, Suchrichtung richtung, ref int anzahl)
    {
        if (richtung is Suchrichtung.Beide or Suchrichtung.Tiefer)
        {
            ParalleleSpiele(t, datum, tag, nummer, Suchrichtung.Tiefer, ref anzahl);
        }

        if (richtung is Suchrichtung.Beide or Suchrichtung.Hoeher)
        {
            ParalleleSpiele(t, datum, tag, nummer, Suchrichtung.Hoeher, ref anzahl);
        }
    }

    private void SechzigKilometer(int t, int k, double faktor)
    {
        if (!D.HatSechzigKilometer)
        {
            return;
        }

        KostenAlle[k] = D.KeinWochenspielAnzahl[t];
        if (D.KeinWochenspielAnzahl[t] == 0)
        {
            return;
        }

        int verletzt = 0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (Heim[s] != t && D.KeinWochenspielGegen[(t * D.N) + Heim[s]] && Aktiv(s) && !D.IstWochenendeFuer60Km(Datum[s]))
            {
                verletzt++;
            }
        }

        if (verletzt == 0)
        {
            return;
        }

        KostenAnzahl[k] = verletzt;
        double wert = verletzt * 10.0;
        KostenWert[k] = Math.Pow(wert, 2) * faktor;
    }
}
