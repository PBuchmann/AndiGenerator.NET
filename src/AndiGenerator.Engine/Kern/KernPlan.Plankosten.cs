// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>Kostenrechnung des Plans (Original <c>TPlan.CalculateKosten</c>) auf Arrays, Summationsreihenfolge wie im Referenzmodell.</summary>
internal sealed partial class KernPlan
{
    private static readonly MannschaftsKostenart[] ReihenfolgeSchnell =
    [
        MannschaftsKostenart.DreiTageAbstand, MannschaftsKostenart.ZweiSpieleProWoche, MannschaftsKostenart.SechzigKilometerRegel,
        MannschaftsKostenart.Auswaertskoppel, MannschaftsKostenart.Pflichtspieltage, MannschaftsKostenart.Heimkoppel,
        MannschaftsKostenart.UngleichHeimAuswaerts, MannschaftsKostenart.WechselHeimAuswaerts, MannschaftsKostenart.AbstandHeimAuswaerts,
        MannschaftsKostenart.Setzliste, MannschaftsKostenart.Sperrtermine,
    ];

    private static readonly MannschaftsKostenart[] ReihenfolgeLangsam =
    [
        MannschaftsKostenart.Ausweichtermine, MannschaftsKostenart.Hallenbelegung, MannschaftsKostenart.ParalleleSpiele,
        MannschaftsKostenart.GleicheHeimtermine, MannschaftsKostenart.Spielverteilung,
    ];

    /// <summary>Original <c>CalculateKosten(-1)</c>.</summary>
    public double Kosten()
    {
        Berechnungsoptionen optionen = D.Optionen;
        AenderungenErmitteln();
        double ergebnis = FehlterminKosten();
        ergebnis += SpielfreieTageKosten();
        if (optionen.VereinsinterneSpieleAmAnfang != Gewichtung.NichtBeruecksichtigen)
        {
            ergebnis += VereinsinterneSpieleAmAnfangKosten();
        }

        foreach (MannschaftsKostenart art in ReihenfolgeSchnell)
        {
            if (optionen.Fuer(art) != Gewichtung.NichtBeruecksichtigen)
            {
                ergebnis += MannschaftsKostenGecacht(art);
            }
        }

        if (optionen.Spieltag != Gewichtung.NichtBeruecksichtigen || optionen.SpieltagUeberlappung != Gewichtung.NichtBeruecksichtigen)
        {
            RefPlan.Spieltagskosten s = SpieltagsKosten();
            ergebnis += s.Breite + s.Ueberlappung + s.LetzteBreite + s.LetzteUeberlappung;
        }

        foreach (MannschaftsKostenart art in ReihenfolgeLangsam)
        {
            if (optionen.Fuer(art) != Gewichtung.NichtBeruecksichtigen)
            {
                ergebnis += MannschaftsKostenGecacht(art);
            }
        }

        return ergebnis + NichtErlaubteKosten();
    }

    /// <summary>Original <c>CalculateMannschaftsKosten</c>.</summary>
    public double MannschaftsKosten(MannschaftsKostenart art)
    {
        double ergebnis = 0;
        for (int t = 0; t < D.N; t++)
        {
            ergebnis += KostenFuerArt(t, art);
        }

        return ergebnis;
    }

    /// <summary>Original <c>CalculateFehlterminKosten</c>.</summary>
    public double FehlterminKosten()
    {
        int anzahl = 0;
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if (Datum[s] == 0.0 && !NichtNotwendig[s])
            {
                anzahl++;
            }
        }

        return anzahl * 10.0 * RefPlan.MaxKostenOhneHartenFehler;
    }

    /// <summary>Original <c>CalculateFreeGameDateKosten</c>.</summary>
    public double SpielfreieTageKosten()
    {
        int fehler = 0;
        if (D.SpielfreieTage.Count > 0)
        {
            for (int s = 0; s < anzahlErlaubt; s++)
            {
                if (D.SpielfreieTage.Contains(Tag[s]) && InPlanung[s])
                {
                    fehler++;
                }
            }
        }

        return fehler * 10.0 * RefPlan.MaxKostenOhneHartenFehler;
    }

    /// <summary>Original <c>CalculateNotAllowedKosten</c>.</summary>
    public double NichtErlaubteKosten() => NichtErlaubteAnzahl() * 10.0 * RefPlan.MaxKostenOhneHartenFehler;

    /// <summary>Anzahl der Spiele aus Original <c>getNotAllowedGames</c>.</summary>
    public int NichtErlaubteAnzahl()
    {
        if (AlleTermineGueltig && !D.HatKoppeltermine && !D.HatAuswaertsKoppeltermine)
        {
            return 0;
        }

        int anzahl = anzahlAktiv - anzahlErlaubt;
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if (Datum[s] != 0.0 && !NichtNotwendig[s] && !SpielIstGueltig(s))
            {
                anzahl++;
            }
        }

        return anzahl;
    }

    /// <summary>Original <c>CalculateSisterTeamsAmAnfang</c>.</summary>
    public double VereinsinterneSpieleAmAnfangKosten()
    {
        int fehler = TeamsNichtAmAnfang();
        double wert = Math.Pow(fehler, 2) * 300 * RefPlan.Gewichtswert(D.Optionen.VereinsinterneSpieleAmAnfang);
        return Math.Min(RefPlan.MaxKostenOhneHartenFehler, wert);
    }

    /// <summary>Original <c>CalculateSpielTagKosten</c>.</summary>
    public RefPlan.Spieltagskosten SpieltagsKosten()
    {
        Berechnungsoptionen optionen = D.Optionen;
        double ueberlappung = 0;
        double breite = 0;
        double letzteBreite = 0;
        double letzteUeberlappung = 0;
        int spieltage = 0;
        for (int t = 0; t < D.N; t++)
        {
            spieltage = Math.Max(spieltage, SpieleAnzahl[t]);
        }

        int anzahlMaxima = 0;
        for (int i = 0; i < spieltage; i++)
        {
            int anzahlTage = 0;
            double max = 0.0;
            double min = 0.0;
            for (int t = 0; t < D.N; t++)
            {
                int s = SpieleAnzahl[t] > i ? Spiele[t][i] : -1;
                double datum = s >= 0 ? Datum[s] : 0.0;
                if (datum != 0.0 && InPlanung[s])
                {
                    tage[anzahlTage++] = DelphiDatum.Trunc(datum);
                    max = Math.Max(max, datum);
                    min = min == 0.0 ? datum : Math.Min(min, datum);
                }
            }

            if (max > 0 && min > 0)
            {
                double faktor = 5;
                for (int j = anzahlMaxima - 1; j >= 0; j--)
                {
                    if (DelphiDatum.Trunc(min) <= DelphiDatum.Trunc(letzteMaxima[j]))
                    {
                        double abstand = 1 + ((DelphiDatum.Trunc(letzteMaxima[j]) - DelphiDatum.Trunc(min)) / 8.0);
                        double wert = abstand * faktor;
                        if (spieltage - 1 == i)
                        {
                            letzteUeberlappung = wert * 20.0;
                        }

                        ueberlappung += wert;
                    }

                    faktor *= 5;
                }

                letzteMaxima[anzahlMaxima++] = max;
            }

            double laenge = DelphiDatum.Trunc(max) - DelphiDatum.Trunc(min);
            laenge *= D.N / 10.0;
            double diff = Math.Max(0, laenge - 6.0) / 7.0;
            if (laenge > 0)
            {
                double sigma = Standardabweichung(tage, anzahlTage);
                diff += Math.Min(0.99, sigma / (laenge / 2));
            }

            if (spieltage - 1 == i)
            {
                letzteBreite = Math.Pow(diff, 3) * RefPlan.Gewichtswert(optionen.LetzterSpieltag) * 5.0;
            }

            breite += Math.Pow(diff, 3) * RefPlan.Gewichtswert(optionen.Spieltag);
        }

        letzteBreite = Math.Min(RefPlan.MaxKostenOhneHartenFehler, letzteBreite);
        breite = Math.Min(RefPlan.MaxKostenOhneHartenFehler, breite);
        ueberlappung = Math.Min(RefPlan.MaxKostenOhneHartenFehler, ueberlappung * RefPlan.Gewichtswert(optionen.SpieltagUeberlappung));
        letzteUeberlappung = Math.Min(RefPlan.MaxKostenOhneHartenFehler, letzteUeberlappung * RefPlan.Gewichtswert(optionen.LetzterSpieltagUeberlappung));
        return new RefPlan.Spieltagskosten(breite, ueberlappung, letzteBreite, letzteUeberlappung);
    }

    /// <summary>Original <c>getTeamsNotAtBegin</c>.</summary>
    private int TeamsNichtAmAnfang()
    {
        int ergebnis = 0;
        for (int t = 0; t < D.N; t++)
        {
            int vereinsteams = D.Vereinsteams[t].Length;
            if (vereinsteams == 0)
            {
                continue;
            }

            for (int r = 0; r < D.Runden.Length; r++)
            {
                if (D.RundeZuPlanen[r])
                {
                    ergebnis += NichtAmAnfangInRunde(t, D.Runden[r], vereinsteams);
                }
            }
        }

        return ergebnis;
    }

    private int NichtAmAnfangInRunde(int t, RefRunde runde, int vereinsteams)
    {
        int ergebnis = 0;
        int gefunden = 0;
        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t] && gefunden < vereinsteams; i++)
        {
            int s = liste[i];
            if (Datum[s] == 0.0 || !runde.Enthaelt(Datum[s]))
            {
                continue;
            }

            if (D.GleicherVerein[(t * D.N) + Heim[s]] || D.GleicherVerein[(t * D.N) + Gast[s]])
            {
                gefunden++;
            }
            else
            {
                ergebnis++;
            }
        }

        return ergebnis;
    }
}
