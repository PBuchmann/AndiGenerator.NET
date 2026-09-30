// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Kostenrechnung des Plans (Original <c>TPlan.CalculateKosten</c> und Hilfsfunktionen), ohne Caches.</summary>
internal sealed partial class RefPlan
{
    /// <summary>Original <c>cMaxKostenNoHardError</c>.</summary>
    public const double MaxKostenOhneHartenFehler = 1000000000.0;

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

    /// <summary>Original <c>CalculateStandardAbweichung</c> (Varianz über n).</summary>
    public static double Standardabweichung(List<double> werte)
    {
        if (werte.Count <= 1)
        {
            return 0;
        }

        double mittel = 0;
        foreach (double wert in werte)
        {
            mittel += wert;
        }

        mittel /= werte.Count * 1.0;
        double summe = 0;
        foreach (double wert in werte)
        {
            double d = wert - mittel;
            summe += d * d;
        }

        return Math.Sqrt(summe / (werte.Count * 1.0));
    }

    /// <summary>Original <c>CalculateKosten(-1)</c> (ohne Abbruchwert).</summary>
    public double Kosten()
    {
        double ergebnis = FehlterminKosten();
        ergebnis += SpielfreieTageKosten();
        if (Optionen.VereinsinterneSpieleAmAnfang != Gewichtung.NichtBeruecksichtigen)
        {
            ergebnis += VereinsinterneSpieleAmAnfangKosten();
        }

        foreach (MannschaftsKostenart art in ReihenfolgeSchnell.Where(a => Optionen.Fuer(a) != Gewichtung.NichtBeruecksichtigen))
        {
            ergebnis += MannschaftsKosten(art);
        }

        if (Optionen.Spieltag != Gewichtung.NichtBeruecksichtigen || Optionen.SpieltagUeberlappung != Gewichtung.NichtBeruecksichtigen)
        {
            Spieltagskosten s = SpieltagsKosten();
            ergebnis += s.Breite + s.Ueberlappung + s.LetzteBreite + s.LetzteUeberlappung;
        }

        foreach (MannschaftsKostenart art in ReihenfolgeLangsam.Where(a => Optionen.Fuer(a) != Gewichtung.NichtBeruecksichtigen))
        {
            ergebnis += MannschaftsKosten(art);
        }

        return ergebnis + NichtErlaubteKosten();
    }

    /// <summary>Original <c>CalculateMannschaftsKosten</c>.</summary>
    public double MannschaftsKosten(MannschaftsKostenart art)
    {
        double ergebnis = 0;
        foreach (RefMannschaft mannschaft in Mannschaften)
        {
            var kosten = new RefMannschaftsKosten();
            mannschaft.KostenFuerArt(this, art, kosten, meldungen: false);
            ergebnis += kosten.Werte[(int)art].GesamtKosten;
        }

        return ergebnis;
    }

    /// <summary>Original <c>CalculateFehlterminKosten</c>.</summary>
    public double FehlterminKosten() =>
        SpieleErlaubt.Count(s => s.Datum == 0.0 && !s.NichtNotwendig) * 10.0 * MaxKostenOhneHartenFehler;

    /// <summary>Original <c>CalculateFreeGameDateKosten</c>.</summary>
    public double SpielfreieTageKosten()
    {
        int fehler = SpielfreieTage.Count > 0 ? SpieleErlaubt.Count(s => IstSpielfreierTag(s.Datum) && IstInZuPlanenderRunde(s.Datum)) : 0;
        return fehler * 10.0 * MaxKostenOhneHartenFehler;
    }

    /// <summary>Original <c>CalculateNotAllowedKosten</c>.</summary>
    public double NichtErlaubteKosten() => NichtErlaubteSpiele().Count * 10.0 * MaxKostenOhneHartenFehler;

    /// <summary>Original <c>getNotAllowedGames</c>.</summary>
    public List<RefSpiel> NichtErlaubteSpiele()
    {
        var ergebnis = new List<RefSpiel>();
        if (!AlleTermineGueltig || HatKoppeltermine() || HatAuswaertsKoppeltermine())
        {
            ergebnis.AddRange(SpieleNichtErlaubt);
            ergebnis.AddRange(SpieleErlaubt.Where(s => s.Datum != 0.0 && !SpielIstGueltig(s) && !s.NichtNotwendig));
        }

        return ergebnis;
    }

    /// <summary>Original <c>CalculateSisterTeamsAmAnfang</c>.</summary>
    public double VereinsinterneSpieleAmAnfangKosten()
    {
        int fehler = TeamsNichtAmAnfang();
        double wert = Math.Pow(fehler, 2) * 300 * Gewichtswert(Optionen.VereinsinterneSpieleAmAnfang);
        return Math.Min(MaxKostenOhneHartenFehler, wert);
    }

    /// <summary>Original <c>CalculateSpielTagKosten</c>.</summary>
    public Spieltagskosten SpieltagsKosten()
    {
        double ueberlappung = 0;
        double breite = 0;
        double letzteBreite = 0;
        double letzteUeberlappung = 0;
        int spieltage = Mannschaften.Count == 0 ? 0 : Mannschaften.Max(m => m.Spiele.Count);
        var letzteMaxima = new List<double>();

        for (int i = 0; i < spieltage; i++)
        {
            var tage = new List<int>();
            double max = 0.0;
            double min = 0.0;
            foreach (List<RefSpiel> spiele in Mannschaften.Select(m => m.Spiele))
            {
                double datum = spiele.Count > i ? spiele[i].Datum : 0.0;
                if (datum != 0.0 && IstInZuPlanenderRunde(datum))
                {
                    tage.Add(DelphiDatum.Trunc(datum));
                    max = Math.Max(max, datum);
                    min = min == 0.0 ? datum : Math.Min(min, datum);
                }
            }

            if (max > 0 && min > 0)
            {
                double faktor = 5;
                for (int j = letzteMaxima.Count - 1; j >= 0; j--)
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

                letzteMaxima.Add(max);
            }

            double laenge = DelphiDatum.Trunc(max) - DelphiDatum.Trunc(min);
            laenge *= Mannschaften.Count / 10.0;
            double diff = Math.Max(0, laenge - 6.0) / 7.0;
            if (laenge > 0)
            {
                double sigma = Standardabweichung(tage.Select(t => (double)t).ToList());
                diff += Math.Min(0.99, sigma / (laenge / 2));
            }

            if (spieltage - 1 == i)
            {
                letzteBreite = Math.Pow(diff, 3) * Gewichtswert(Optionen.LetzterSpieltag) * 5.0;
            }

            breite += Math.Pow(diff, 3) * Gewichtswert(Optionen.Spieltag);
        }

        letzteBreite = Math.Min(MaxKostenOhneHartenFehler, letzteBreite);
        breite = Math.Min(MaxKostenOhneHartenFehler, breite);
        ueberlappung = Math.Min(MaxKostenOhneHartenFehler, ueberlappung * Gewichtswert(Optionen.SpieltagUeberlappung));
        letzteUeberlappung = Math.Min(MaxKostenOhneHartenFehler, letzteUeberlappung * Gewichtswert(Optionen.LetzterSpieltagUeberlappung));
        return new Spieltagskosten(breite, ueberlappung, letzteBreite, letzteUeberlappung);
    }

    /// <summary>Original <c>GameIsValid</c>.</summary>
    public bool SpielIstGueltig(RefSpiel spiel)
    {
        if (!IstInZuPlanenderRunde(spiel.Datum))
        {
            return Optionen.Rundenplanung is Rundenplanung.NurRueckrunde or Rundenplanung.Corona
                && (BestehenderSpielplan.Exists(s => s.IstGleich(spiel)) || VorgegebeneSpiele.Exists(s => s.IstGleich(spiel)));
        }

        RefWunschtermin? wunsch = spiel.Heim.Wunschtermine.Find(w => w.Datum == spiel.Datum);
        if (wunsch is not null)
        {
            return TerminIstGueltig(wunsch, spiel, spiel, false);
        }

        return VorgegebeneSpiele.Exists(s => s.IstGleich(spiel));
    }

    /// <summary>Original <c>TerminIsValid</c>.</summary>
    public bool TerminIstGueltig(RefWunschtermin termin, RefSpiel spiel, RefSpiel? ignorieren, bool alleZweitzeitenGueltig)
    {
        if (SpielfreieTage.Count > 0 && IstSpielfreierTag(termin.Datum))
        {
            return false;
        }

        if (!spiel.Heim.IstTerminFrei(termin, this, spiel.Heim, spiel.Gast, ignorieren, alleZweitzeitenGueltig)
            || !spiel.Gast.IstTerminFrei(termin, this, spiel.Heim, spiel.Gast, ignorieren, alleZweitzeitenGueltig))
        {
            return false;
        }

        int runde = RundeZuDatum(termin.Datum);
        List<RefSpiel>? gleiche = SpieleDerPaarung(spiel.Heim.TeamId, spiel.Gast.TeamId);
        if (gleiche is not null && gleiche.Exists(t => !ReferenceEquals(t, ignorieren) && t.DatumGueltig && runde / 2 == RundeZuDatum(t.Datum) / 2))
        {
            return false;
        }

        List<RefSpiel>? rueck = SpieleDerPaarung(spiel.Gast.TeamId, spiel.Heim.TeamId);
        return rueck is null || !rueck.Exists(t => !ReferenceEquals(t, ignorieren) && t.DatumGueltig && runde == RundeZuDatum(t.Datum));
    }

    /// <summary>Original <c>getNumSollGamesIntern</c>.</summary>
    public double SollSpiele(double von, double bis)
    {
        int runde = RundeZuDatum(von);
        int anzahlTermine = 0;
        int imIntervall = 0;
        foreach (RefWunschtermin w in Mannschaften.SelectMany(m => m.Wunschtermine).Where(w => runde == RundeZuDatum(w.Datum) && !IstSpielfreierTag(w.Datum)))
        {
            int anzahl = Terminoptionen.IstKoppel(w.Optionen) ? 2 : 1;
            anzahlTermine += anzahl;
            if (DelphiDatum.Trunc(von) <= DelphiDatum.Trunc(w.Datum) && DelphiDatum.Trunc(bis) > DelphiDatum.Trunc(w.Datum))
            {
                imIntervall += anzahl;
            }
        }

        if (anzahlTermine > 0 && Mannschaften.Count > 0)
        {
            double faktor = (Mannschaften.Count - 1) / (1.0 * anzahlTermine);
            return faktor * imIntervall;
        }

        return 0;
    }

    /// <summary>Original <c>getFirstRealDateInRound</c>.</summary>
    public double ErstesEchtesDatumInRunde(int runde)
    {
        double ergebnis = 0;
        foreach (double datum in Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => w.Datum))
        {
            if ((ergebnis == 0.0 || datum < ergebnis) && runde == RundeZuDatum(datum) && !IstSpielfreierTag(datum))
            {
                ergebnis = datum;
            }
        }

        return ergebnis;
    }

    /// <summary>Original <c>getLastRealDateInRound</c>.</summary>
    public double LetztesEchtesDatumInRunde(int runde)
    {
        List<double> daten = Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => w.Datum)
            .Where(d => d > 0 && runde == RundeZuDatum(d) && !IstSpielfreierTag(d))
            .ToList();
        return daten.Count == 0 ? 0 : daten.Max();
    }

    /// <summary>Original <c>CalcHasKoppelTermine</c>.</summary>
    public bool HatKoppeltermine() => Mannschaften.SelectMany(m => m.Wunschtermine).Any(w => Terminoptionen.IstKoppel(w.Optionen));

    /// <summary>Original <c>CalcHasAuswaertsKoppelTermine</c>.</summary>
    public bool HatAuswaertsKoppeltermine() => Mannschaften.Exists(m => m.Auswaertskoppeln.Count > 0);

    /// <summary>Original <c>CalcHas60KilometerValues</c>.</summary>
    public bool HatSechzigKilometerWerte() => Mannschaften.Exists(m => m.KeinWochenspiel.Count > 0);

    /// <summary>Original <c>CalcHasAusweichTermine</c>.</summary>
    public bool HatAusweichtermine() => Mannschaften.Exists(m => m.Ausweichtermine.Count > 0);

    /// <summary>Original <c>CalcHasForceSameHomeGames</c>.</summary>
    public bool HatGleicheHeimspieltage() =>
        Mannschaften.SelectMany(m => m.Nachbarspiele.Values).SelectMany(l => l).Any(s => s.ParalleleHeimspiele);

    /// <summary>Original <c>HasRanking</c>.</summary>
    public bool HatSetzliste() => Setzlistenplaetze.Count > 0;

    /// <summary>Original <c>CalcNumWunschDays</c>.</summary>
    public int AnzahlWunschtage() =>
        Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => DelphiDatum.Trunc(w.Datum)).Distinct().Count();

    /// <summary>Original <c>HasValuesForType</c>: Spalte in der Kostentabelle sichtbar.</summary>
    public bool HatWerteFuer(MannschaftsKostenart art) => art switch
    {
        MannschaftsKostenart.SechzigKilometerRegel => HatSechzigKilometerWerte(),
        MannschaftsKostenart.Heimkoppel => HatKoppeltermine(),
        MannschaftsKostenart.Ausweichtermine => HatAusweichtermine(),
        MannschaftsKostenart.Auswaertskoppel => HatAuswaertsKoppeltermine(),
        MannschaftsKostenart.Pflichtspieltage => Pflichtzeiten.Count > 0,
        MannschaftsKostenart.Setzliste => HatSetzliste(),
        MannschaftsKostenart.GleicheHeimtermine => HatGleicheHeimspieltage(),
        _ => true,
    };

    /// <summary>Original <c>NoGames</c>.</summary>
    public bool KeineSpiele() => SpieleErlaubt.TrueForAll(s => s.Datum == 0);

    /// <summary>Original <c>getTeamsNotAtBegin</c>.</summary>
    private int TeamsNichtAmAnfang()
    {
        int ergebnis = 0;
        foreach (RefMannschaft mannschaft in Mannschaften.Where(m => m.VereinsteamsImPlan.Count > 0))
        {
            foreach (RefRunde runde in Runden.Where(IstZuPlanendeRunde))
            {
                int gefunden = 0;
                foreach (RefSpiel spiel in mannschaft.Spiele.Where(s => s.DatumGueltig))
                {
                    if (runde.Enthaelt(spiel.Datum))
                    {
                        if (mannschaft.VereinsteamsImPlan.Contains(spiel.Heim) || mannschaft.VereinsteamsImPlan.Contains(spiel.Gast))
                        {
                            gefunden++;
                        }
                        else
                        {
                            ergebnis++;
                        }
                    }

                    if (gefunden >= mannschaft.VereinsteamsImPlan.Count)
                    {
                        break;
                    }
                }
            }
        }

        return ergebnis;
    }

    /// <summary>Ergebnis von <see cref="SpieltagsKosten"/>.</summary>
    public readonly record struct Spieltagskosten(double Breite, double Ueberlappung, double LetzteBreite, double LetzteUeberlappung);
}
