// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Kostenarten je Mannschaft (Original <c>TMannschaft.CalculateKosten*</c>), ohne Kosten-Cache.</summary>
internal sealed partial class RefMannschaft
{
    private const int MaxMannschaft = 30;
    private const int MaxRunde = 10;

    /// <summary>Original <c>CalculateKosten</c>: alle Kostenarten.</summary>
    public void KostenBerechnen(RefPlan plan, RefMannschaftsKosten kosten, bool meldungen)
    {
        foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
        {
            KostenFuerArt(plan, art, kosten, meldungen);
        }
    }

    /// <summary>Original <c>CalculateKostenForType</c>.</summary>
    public void KostenFuerArt(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        kosten.Leeren(art);
        switch (art)
        {
            case MannschaftsKostenart.Hallenbelegung: Hallenbelegung(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.ParalleleSpiele: ParalleleSpieleKosten(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.GleicheHeimtermine: GleicheHeimtermine(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.Sperrtermine: SperrtermineKosten(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.Ausweichtermine: AusweichtermineKosten(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.SechzigKilometerRegel: SechzigKilometer(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.DreiTageAbstand: EngeTermine(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.ZweiSpieleProWoche: ZweiSpieleDieWoche(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.Spielverteilung: Spielverteilung(plan, art, kosten); break;
            case MannschaftsKostenart.Heimkoppel: Koppeltermine(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.Auswaertskoppel: AuswaertsKoppeltermine(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.UngleichHeimAuswaerts: ZahlHeimspiele(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.WechselHeimAuswaerts: WechselHeimAuswaerts(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.AbstandHeimAuswaerts: AbstandHeimAuswaerts(plan, art, kosten, meldungen); break;
            case MannschaftsKostenart.Setzliste: Setzliste(plan, art, kosten, meldungen); break;
            default: Pflichtspieltage(plan, art, kosten, meldungen); break;
        }
    }

    /// <summary>Original <c>CalculateMaxAbweichung</c>.</summary>
    private static (int Echt, double Korrigiert, double Mittel) MaxAbweichung(List<int> werte, double faktorKleiner, double faktorGroesser)
    {
        int echt = 0;
        double korrigiert = 0;
        if (werte.Count <= 1)
        {
            return (echt, korrigiert, 1);
        }

        double mittel = 0;
        foreach (int wert in werte)
        {
            mittel += wert;
        }

        mittel /= werte.Count * 1.0;
        foreach (int wert in werte)
        {
            double faktor = wert < mittel ? faktorKleiner : faktorGroesser;
            korrigiert = Math.Max(faktor * Math.Abs(wert - mittel), korrigiert);
            echt = Math.Max(DelphiDatum.Trunc(Math.Abs(wert - mittel) + 0.5), echt);
        }

        return (echt, korrigiert, mittel);
    }

    private static string Zahl(double wert) => Kostenanzeige.Format(wert);

    private RefMannschaft Gegner(RefSpiel spiel) => ReferenceEquals(spiel.Heim, this) ? spiel.Gast : spiel.Heim;

    private void AbstandHeimAuswaerts(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        Berechnungsoptionen o = plan.Optionen;
        if (o.Rundenplanung is Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde or Rundenplanung.Corona && !o.Doppelrunde)
        {
            return;
        }

        if (plan.Mannschaften.Count > MaxMannschaft)
        {
            throw new InvalidOperationException($"Der Plan enthält zu viele Mannschaften. Für maximal {MaxMannschaft} kann der Plan generiert werden");
        }

        var positionen = new List<int>[plan.Mannschaften.Count];
        for (int i = 0; i < Spiele.Count; i++)
        {
            if (!Spiele[i].DatumGueltig)
            {
                continue;
            }

            int gegner = Gegner(Spiele[i]).Index;
            positionen[gegner] ??= [];
            if (positionen[gegner].Count >= MaxRunde - 1)
            {
                continue;
            }

            positionen[gegner].Add(i);
        }

        var werte = new List<int>();
        for (int j = 0; j < plan.Mannschaften.Count; j++)
        {
            if (j == Index || positionen[j] is null)
            {
                continue;
            }

            for (int i = 0; i < positionen[j].Count - 1; i++)
            {
                werte.Add(positionen[j][i + 1] - positionen[j][i]);
            }
        }

        (int echt, double wert, double mittel) = MaxAbweichung(werte, 2, 1);
        kosten.Werte[(int)art].Anzahl = echt;
        if (meldungen)
        {
            kosten.Meldungen.Add("Maximale Abweichung Reihenfolge Heim- und Auswärtsspiel: " + Zahl(echt));
        }

        wert /= mittel;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert * 6, 6) / 5000.0 * plan.MannschaftKostenFaktor(this, art);
    }

    private void Setzliste(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (!plan.HatSetzliste())
        {
            return;
        }

        List<int> diffs = SetzlistenAbstaende(plan);
        int ziel = plan.Mannschaften.Count - 1;
        int runde = -1;
        double wert = 0;
        int anzahl = 0;
        double faktor = 1.0;
        for (int i = 0; i < Spiele.Count; i++)
        {
            if (!Spiele[i].DatumGueltig)
            {
                continue;
            }

            int spielRunde = plan.RundeZuDatum(Spiele[i].Datum);
            if (runde != spielRunde)
            {
                ziel = plan.Mannschaften.Count - 1;
                runde = spielRunde;
                if (runde == plan.Runden.Count - 1)
                {
                    faktor = 4.0;
                }
            }
            else
            {
                ziel--;
            }

            int diff = diffs[i] - ziel;
            if (diff > 0 && ziel > 0)
            {
                wert += faktor * (diff / Math.Pow(ziel, 1.25));
                anzahl++;
            }
        }

        kosten.Werte[(int)art].Anzahl = anzahl;
        _ = meldungen;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert / 8.0, 3) * plan.MannschaftKostenFaktor(this, art);
    }

    /// <summary>Original <c>getRankingDiffs</c>.</summary>
    private List<int> SetzlistenAbstaende(RefPlan plan)
    {
        var ergebnis = new List<int>();
        int eigenerRang = plan.Setzlistenplaetze.IndexOf(Index);
        foreach (RefSpiel spiel in Spiele)
        {
            int gegnerRang = plan.Setzlistenplaetze.IndexOf(Gegner(spiel).Index);
            ergebnis.Add(gegnerRang < 0 || eigenerRang < 0 ? 0 : Math.Abs(eigenerRang - gegnerRang));
        }

        return ergebnis;
    }

    private void Pflichtspieltage(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        int alle = 0;
        foreach (RefPflichtzeit zeit in plan.Pflichtzeiten)
        {
            if (!plan.IstInZuPlanenderRunde(zeit.Von))
            {
                continue;
            }

            alle += zeit.AnzahlSpiele;
            int anzahl = 0;
            for (int i = ErsterIndexAb(zeit.Von); i < Spiele.Count; i++)
            {
                if (!Spiele[i].DatumGueltig)
                {
                    continue;
                }

                int tag = DelphiDatum.Trunc(Spiele[i].Datum);
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
                kosten.Werte[(int)art].Anzahl += zeit.AnzahlSpiele - anzahl;
                if (meldungen)
                {
                    kosten.Meldungen.Add($"{zeit.AnzahlSpiele - anzahl} Spiel(e) zu wenig vom {DelphiDatum.Text(zeit.Von)} - {DelphiDatum.Text(zeit.Bis)}");
                }
            }
        }

        double wert = kosten.Werte[(int)art].Anzahl * 1.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * 1000 * plan.MannschaftKostenFaktor(this, art);
        kosten.Werte[(int)art].AllCount = alle;
    }

    /// <summary>Original <c>TGameList.getFirstGameRefIndexByDate</c>.</summary>
    private int ErsterIndexAb(double datum)
    {
        int l = 0;
        int h = Spiele.Count - 1;
        while (l <= h)
        {
            int i = (l + h) >> 1;
            int c = Spiele[i].Datum.CompareTo(datum);
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

    private void WechselHeimAuswaerts(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        int gleich = 0;
        bool letztesHeim = false;
        double wert = 0;
        for (int i = 0; i < Spiele.Count; i++)
        {
            if (!Spiele[i].DatumGueltig || !plan.IstInZuPlanenderRunde(Spiele[i].Datum))
            {
                continue;
            }

            bool heim = ReferenceEquals(Spiele[i].Heim, this);
            if (i != 0)
            {
                if (heim == letztesHeim)
                {
                    if (!ReferenceEquals(Spiele[i - 1], GekoppeltesSpiel(i)))
                    {
                        gleich++;
                        kosten.Werte[(int)art].Anzahl++;
                        wert += Math.Pow(gleich, 4);
                    }
                }
                else
                {
                    gleich = 0;
                }
            }

            letztesHeim = heim;
        }

        if (meldungen)
        {
            kosten.Meldungen.Add("Kein Wechsel Heimspiel und Auswärtsspiel: " + kosten.Werte[(int)art].Anzahl);
        }

        kosten.Werte[(int)art].GesamtKosten = (Math.Pow(wert, 2) / 30.0) * plan.MannschaftKostenFaktor(this, art);
    }

    private void ZahlHeimspiele(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        Berechnungsoptionen o = plan.Optionen;
        bool rundenIgnorieren = o.Rundenplanung == Rundenplanung.Corona || (o.Rundenplanung == Rundenplanung.Halbrunde && !o.Doppelrunde);
        int[] jeRunde = new int[rundenIgnorieren ? 1 : plan.Runden.Count];

        int heimrechtFehler = 0;
        if (Heimrechte.Count > 0)
        {
            foreach (RefSpiel spiel in Spiele.Where(s => s.DatumGueltig))
            {
                bool heim = ReferenceEquals(spiel.Heim, this);
                HeimrechtWert recht = HeimrechtGegen(Gegner(spiel).TeamName);
                if (recht == HeimrechtWert.Keins)
                {
                    continue;
                }

                int runde = rundenIgnorieren ? 0 : plan.RundeZuDatum(spiel.Datum);
                bool zielHeim = runde % 2 == 0;
                if (recht == HeimrechtWert.Runde2)
                {
                    zielHeim = !zielHeim;
                }

                if (heim != zielHeim)
                {
                    heimrechtFehler++;
                    if (meldungen)
                    {
                        kosten.Meldungen.Add(spiel.Text() + (zielHeim ? " sollte ein Heimspiel sein" : " sollte ein Auswärtsspiel sein"));
                    }
                }
            }
        }

        foreach (RefSpiel spiel in Spiele.Where(s => s.DatumGueltig && ReferenceEquals(s.Heim, this)))
        {
            int runde = rundenIgnorieren ? 0 : plan.RundeZuDatum(spiel.Datum);
            if (runde >= 0)
            {
                jeRunde[runde]++;
            }
        }

        double ziel;
        if (rundenIgnorieren)
        {
            ziel = Spiele.Count(s => !s.NichtNotwendig) * 0.5 / 1;
        }
        else
        {
            ziel = Spiele.Count * 0.5 / plan.Runden.Count;
            if (o.Rundenplanung == Rundenplanung.Halbrunde && o.Doppelrunde)
            {
                ziel = Spiele.Count * 0.25 / plan.Runden.Count;
            }
        }

        int anzahl = 0;
        if (rundenIgnorieren)
        {
            anzahl += DelphiDatum.Trunc(Math.Abs(jeRunde[0] - ziel));
        }
        else
        {
            for (int i = 0; i < plan.Runden.Count; i++)
            {
                if (!plan.IstZuPlanendeRunde(plan.Runden[i]))
                {
                    continue;
                }

                if (HeimspieleRundeEins >= 0)
                {
                    ziel = i % 2 == 0 ? HeimspieleRundeEins : plan.Mannschaften.Count - HeimspieleRundeEins - 1;
                }

                anzahl += DelphiDatum.Trunc(Math.Abs(jeRunde[i] - ziel));
            }
        }

        double wert = anzahl + (heimrechtFehler * 2);
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * 300 * plan.MannschaftKostenFaktor(this, art);
        kosten.Werte[(int)art].Anzahl = anzahl + heimrechtFehler;
    }

    private void Koppeltermine(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (!plan.HatKoppeltermine())
        {
            return;
        }

        const double niedrig = 0.25;
        int n = MaxRunde + 1;
        int[] hoch = new int[n];
        int[] weich = new int[n];
        int[] erreichtHoch = new int[n];
        int[] erreichtWeich = new int[n];
        double[] optimum = new double[n];
        double[] erreicht = new double[n];

        int alle = 0;
        foreach (RefWunschtermin w in Wunschtermine.Where(w => Terminoptionen.IstKoppel(w.Optionen) && plan.IstInZuPlanenderRunde(w.Datum)))
        {
            alle++;
            int runde = plan.RundeZuDatum(w.Datum);
            if (Terminoptionen.IstKoppelHart(w.Optionen))
            {
                hoch[runde]++;
            }

            if (Terminoptionen.IstKoppelWeich(w.Optionen))
            {
                weich[runde]++;
            }
        }

        if (alle <= 0)
        {
            return;
        }

        int maxJeRunde = (plan.Mannschaften.Count - 1) / 4;
        for (int i = 0; i < plan.Runden.Count; i++)
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

        for (int i = 0; i < Spiele.Count; i++)
        {
            if (!Spiele[i].DatumGueltig || !plan.IstInZuPlanenderRunde(Spiele[i].Datum))
            {
                continue;
            }

            Wunschterminoptionen koppel = KoppelOptionen(Spiele[i]);
            if (Terminoptionen.Hat(koppel, Wunschterminoptionen.DoppelMoeglich) || Terminoptionen.Hat(koppel, Wunschterminoptionen.KoppelMoeglich))
            {
                continue;
            }

            if (Terminoptionen.IstKoppel(koppel) && GekoppeltesSpielDanach(i) is not null)
            {
                int runde = plan.RundeZuDatum(Spiele[i].Datum);
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
        for (int i = 0; i < plan.Runden.Count; i++)
        {
            if (optimum[i] > erreicht[i])
            {
                wert += optimum[i] - erreicht[i];
            }

            anzahl += erreichtHoch[i] + erreichtWeich[i];
            gesamt += hoch[i] + weich[i];
        }

        kosten.Werte[(int)art].Anzahl = gesamt - anzahl;
        kosten.Werte[(int)art].AllCount = gesamt;
        if (meldungen)
        {
            int optHoch = hoch.Sum();
            int realHoch = erreichtHoch.Sum();
            int optWeich = weich.Sum();
            int realWeich = erreichtWeich.Sum();
            if (optHoch > realHoch)
            {
                kosten.Meldungen.Add($"Unbedingte Heimkoppeltermine/Doppelspieltage wurden nicht vergeben: {optHoch - realHoch} mal");
            }

            if (optWeich > realWeich)
            {
                kosten.Meldungen.Add($"\"Wenn möglich\" Heimkoppeltermine/Doppelspieltage wurden nicht vergeben: {optWeich - realWeich} mal");
            }
        }

        wert *= 10.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * plan.MannschaftKostenFaktor(this, art);
    }

    private void AuswaertsKoppeltermine(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        kosten.Werte[(int)art].AllCount = 0;
        if (!plan.HatAuswaertsKoppeltermine() || Auswaertskoppeln.Count == 0)
        {
            return;
        }

        int[] zaehler = new int[Math.Max(MaxMannschaft + 1, plan.Mannschaften.Count)];
        int zielwert = plan.Runden.Count / 2;
        foreach (RefAuswaertskoppel koppel in Auswaertskoppeln)
        {
            RefMannschaft? a = koppel.MannschaftA(plan);
            RefMannschaft? b = koppel.MannschaftB(plan);
            if (a is not null && b is not null)
            {
                zaehler[a.Index] = zielwert;
                zaehler[b.Index] = zielwert;
            }
        }

        int wunschAnzahl = zaehler.Count(z => z > 0);
        for (int i = 0; i < Spiele.Count; i++)
        {
            if (ReferenceEquals(Spiele[i].Gast, this) && GekoppeltesSpielDanach(i) is RefSpiel zweites)
            {
                zaehler[Spiele[i].Heim.Index]--;
                zaehler[zweites.Heim.Index]--;
            }
        }

        for (int i = 0; i < zaehler.Length; i++)
        {
            if (zaehler[i] > 0)
            {
                kosten.Werte[(int)art].Anzahl += zaehler[i];
                if (meldungen)
                {
                    kosten.Meldungen.Add("Kein Auswärtskoppeltermin bei: " + plan.Mannschaften[i].TeamName);
                }
            }
        }

        double faktor = Math.Max(0, Math.Min(6, wunschAnzahl - 2)) / 6.0;
        faktor = 1.0 - (0.75 * faktor);
        double wert = kosten.Werte[(int)art].Anzahl * 5.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * 4.0 * plan.MannschaftKostenFaktor(this, art) * faktor;
        kosten.Werte[(int)art].Anzahl /= 2;
        kosten.Werte[(int)art].AllCount = wunschAnzahl / 2;
    }

    private void ZweiSpieleDieWoche(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        for (int i = 0; i < Spiele.Count - 1; i++)
        {
            if (!Spiele[i].DatumGueltig || !plan.IstInZuPlanenderRunde(Spiele[i].Datum))
            {
                continue;
            }

            if (DelphiDatum.InterneWochennummer(Spiele[i + 1].Datum) == DelphiDatum.InterneWochennummer(Spiele[i].Datum)
                && !ReferenceEquals(Spiele[i + 1], GekoppeltesSpiel(i)))
            {
                kosten.Werte[(int)art].Anzahl++;
                if (meldungen)
                {
                    kosten.Meldungen.Add($"Zwei Spiele die Woche: {DelphiDatum.Text(Spiele[i].Datum)} - {DelphiDatum.Text(Spiele[i + 1].Datum)}");
                }
            }
        }

        double wert = kosten.Werte[(int)art].Anzahl * 1.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * 400 * plan.MannschaftKostenFaktor(this, art);
    }

    private void EngeTermine(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        const double fehlerPotenz = 4.3;
        double wert = 0.0;
        for (int i = 0; i < Spiele.Count - 1; i++)
        {
            if (!Spiele[i].DatumGueltig || !plan.IstInZuPlanenderRunde(Spiele[i].Datum))
            {
                continue;
            }

            int tage = DelphiDatum.Trunc(Spiele[i + 1].Datum) - DelphiDatum.Trunc(Spiele[i].Datum);
            if (tage is <= 3 and >= 0 && !ReferenceEquals(Spiele[i + 1], GekoppeltesSpiel(i)))
            {
                kosten.Werte[(int)art].Anzahl++;
                wert += Math.Pow(4.0 / (tage + 1), fehlerPotenz);
                if (meldungen)
                {
                    kosten.Meldungen.Add($"Weniger als drei Tage Abstand: {DelphiDatum.Text(Spiele[i].Datum)} - {DelphiDatum.Text(Spiele[i + 1].Datum)}");
                }
            }
        }

        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * plan.MannschaftKostenFaktor(this, art);
    }

    private void Spielverteilung(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten)
    {
        var werte = new List<double>();
        if (Spiele.Count > 0)
        {
            for (int runde = 0; runde < plan.Runden.Count; runde++)
            {
                if (plan.IstZuPlanendeRunde(plan.Runden[runde]))
                {
                    SpielverteilungRunde(plan, runde, werte);
                }
            }
        }

        double wert = 0.0;
        if (werte.Count > 0)
        {
            wert = RefPlan.Standardabweichung(werte);
        }

        wert = Math.Pow(wert * 5, 5) / 10.0 * plan.MannschaftKostenFaktor(this, art);
        wert = wert / Spiele.Count * 20;
        kosten.Werte[(int)art].Anzahl = -1;
        kosten.Werte[(int)art].GesamtKosten = wert;
    }

    private void SpielverteilungRunde(RefPlan plan, int runde, List<double> werte)
    {
        for (int i = -1; i < Spiele.Count; i++)
        {
            bool erstes = false;
            bool letztes = false;
            double ende;
            double start;

            if (i < 0 || runde > plan.RundeZuDatum(Spiele[i].Datum))
            {
                if (i + 1 >= Spiele.Count || runde != plan.RundeZuDatum(Spiele[i + 1].Datum))
                {
                    continue;
                }

                erstes = true;
                start = plan.ErstesEchtesDatumInRunde(runde);
                ende = Spiele[i + 1].Datum;
            }
            else if (i >= Spiele.Count - 1 || runde < plan.RundeZuDatum(Spiele[i + 1].Datum))
            {
                if (runde != plan.RundeZuDatum(Spiele[i].Datum))
                {
                    continue;
                }

                letztes = true;
                ende = plan.LetztesEchtesDatumInRunde(runde) + 1;
                start = Spiele[i].Datum;
            }
            else
            {
                start = Spiele[i].Datum;
                ende = Spiele[i + 1].Datum;
            }

            if (ende > 0 && start > 0)
            {
                double soll = plan.SollSpiele(start, ende);
                if (!letztes && GekoppeltesSpielDanach(i + 1) is not null)
                {
                    soll /= 1.5;
                }

                if (!erstes && GekoppeltesSpielDavor(i) is not null)
                {
                    soll /= 1.5;
                }

                if (erstes || letztes)
                {
                    soll += 0.5;
                }

                werte.Add(soll);
            }
        }
    }

    private void GleicheHeimtermine(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (TeamsFuerGleicheHeimspieltage().Count == 0)
        {
            return;
        }

        foreach (double datum in Spiele.Where(s => s.DatumGueltig && plan.IstInZuPlanenderRunde(s.Datum) && ReferenceEquals(s.Heim, this)).Select(s => s.Datum))
        {
            int fehlend = FehlendeGleichzeitigeHeimspiele(datum);
            if (fehlend > 0)
            {
                kosten.Werte[(int)art].Anzahl += fehlend;
                if (meldungen)
                {
                    kosten.Meldungen.Add("Kein gleichzeitiges Heimspiel: " + DelphiDatum.Text(datum));
                }
            }
        }

        double wert = kosten.Werte[(int)art].Anzahl;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 3) * 10 * plan.MannschaftKostenFaktor(this, art);
    }

    private void SperrtermineKosten(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        kosten.Werte[(int)art].AllCount = Sperrtermine.Count;
        if (Sperrtermine.Count == 0)
        {
            return;
        }

        List<double> verletzt = Spiele
            .Select(s => (double)DelphiDatum.Trunc(s.Datum))
            .Where(tag => plan.IstInZuPlanenderRunde(tag) && Sperrtermine.BinarySearch(tag) >= 0)
            .ToList();

        if (verletzt.Count == 0)
        {
            return;
        }

        if (meldungen)
        {
            kosten.Meldungen.AddRange(verletzt.Select(t => "Sperrtermin wurde verletzt: " + DelphiDatum.Text(t)));
        }

        kosten.Werte[(int)art].Anzahl = verletzt.Count;
        double wert = verletzt.Count * 1.0 / Sperrtermine.Count * 20.0;
        wert *= plan.AnzahlWunschtage() * 1.0 / 50.0;
        wert = Math.Pow(wert, 3) * 200;
        kosten.Werte[(int)art].GesamtKosten = wert * plan.MannschaftKostenFaktor(this, art);
    }

    private void AusweichtermineKosten(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (!plan.HatAusweichtermine())
        {
            return;
        }

        kosten.Werte[(int)art].AllCount = Ausweichtermine.Count;
        if (Ausweichtermine.Count > 0)
        {
            foreach (RefSpiel spiel in Spiele.Where(s => ReferenceEquals(s.Heim, this) && plan.IstInZuPlanenderRunde(s.Datum) && IstAusweichtermin(DelphiDatum.Trunc(s.Datum))))
            {
                kosten.Werte[(int)art].Anzahl++;
                if (meldungen)
                {
                    kosten.Meldungen.Add("Ausweichtermin wurde verwendet: " + DelphiDatum.Text(spiel.Datum));
                }
            }
        }

        double wert = kosten.Werte[(int)art].Anzahl * 1.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * 10 * plan.MannschaftKostenFaktor(this, art);
    }

    private void Hallenbelegung(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (Nachbarspiele.Count == 0 && VereinsteamsImPlan.Count == 0)
        {
            return;
        }

        foreach (RefSpiel spiel in Spiele.Where(s => s.DatumGueltig && plan.IstInZuPlanenderRunde(s.Datum) && s.DatumMaxHeim > 0 && ReferenceEquals(s.Heim, this)))
        {
            List<string>? belegtVon = meldungen ? [] : null;
            int belegung = Hallenbelegung(spiel.Datum, spiel.Lokal, plan, belegtVon);
            if (belegung >= spiel.DatumMaxHeim)
            {
                kosten.Werte[(int)art].Anzahl += belegung - spiel.DatumMaxHeim + 1;
                if (meldungen)
                {
                    kosten.Meldungen.Add($"Halle belegt von {string.Join(", ", belegtVon!)}: {DelphiDatum.Text(spiel.Datum)}");
                }
            }
        }

        double wert = kosten.Werte[(int)art].Anzahl * 10.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * 500 * plan.MannschaftKostenFaktor(this, art);
    }

    private void ParalleleSpieleKosten(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (Nachbarspiele.Count == 0 && VereinsteamsImPlan.Count == 0)
        {
            return;
        }

        double wert = 0;
        foreach (double datum in Spiele.Where(s => s.DatumGueltig && plan.IstInZuPlanenderRunde(s.Datum)).Select(s => s.Datum))
        {
            int anzahl = ParalleleSpiele(datum, plan);
            if (anzahl > 0)
            {
                kosten.Werte[(int)art].Anzahl += anzahl;
                wert += Math.Pow(anzahl, 3);
                if (meldungen)
                {
                    kosten.Meldungen.Add("Parallele Spiele: " + DelphiDatum.Text(datum));
                }
            }
        }

        wert *= 1.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 1.5) * 20.0 * plan.MannschaftKostenFaktor(this, art);
    }

    private void SechzigKilometer(RefPlan plan, MannschaftsKostenart art, RefMannschaftsKosten kosten, bool meldungen)
    {
        if (!plan.HatSechzigKilometerWerte())
        {
            return;
        }

        kosten.Werte[(int)art].AllCount = KeinWochenspiel.Count;
        if (KeinWochenspiel.Count == 0)
        {
            return;
        }

        List<RefSpiel> verletzt = Spiele
            .Where(s => s.DatumGueltig && plan.IstInZuPlanenderRunde(s.Datum) && !ReferenceEquals(s.Heim, this)
                && KeinWochenspiel.Contains(s.Heim.TeamName) && !plan.IstWochenendeFuer60Km(s.Datum))
            .ToList();
        if (verletzt.Count == 0)
        {
            return;
        }

        kosten.Werte[(int)art].Anzahl = verletzt.Count;
        if (meldungen)
        {
            kosten.Meldungen.AddRange(verletzt.Select(s => $"60km Regel nicht beachtet: {DelphiDatum.Text(s.Datum)} beim {s.Heim.TeamName}"));
        }

        double wert = kosten.Werte[(int)art].Anzahl * 10.0;
        kosten.Werte[(int)art].GesamtKosten = Math.Pow(wert, 2) * plan.MannschaftKostenFaktor(this, art);
    }
}
