// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Original <c>TPlan</c> für die Bewertung eines Plans: Laden (<c>TPlan.load</c>), Optionen anwenden (<c>AssignOptions</c>),
/// Runden, Spiele und Gültigkeit. Die Kostenrechnung steht in <c>RefPlan.Kosten.cs</c>.
/// </summary>
internal sealed partial class RefPlan
{
    private readonly Dictionary<string, List<RefSpiel>> spieleJePaarung = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MannschaftsGewichtung> gewichtungen = new(StringComparer.Ordinal);

    private RefPlan(Berechnungsoptionen optionen)
    {
        Optionen = optionen;
    }

    public Berechnungsoptionen Optionen { get; private set; }

    public string Geschlecht { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public double Beginn { get; private set; }

    public double Ende { get; private set; }

    public double RueckrundenbeginnXml { get; private set; }

    public List<RefMannschaft> Mannschaften { get; } = [];

    public List<RefSpiel> SpieleErlaubt { get; } = [];

    public List<RefSpiel> SpieleNichtErlaubt { get; } = [];

    public List<RefSpiel> VorgegebeneSpiele { get; } = [];

    public List<RefSpiel> BestehenderSpielplan { get; } = [];

    public HashSet<int> SpielfreieTage { get; } = [];

    public List<RefPflichtzeit> Pflichtzeiten { get; } = [];

    public List<int> Setzlistenplaetze { get; } = [];

    public List<RefRunde> Runden { get; } = [];

    public bool AlleTermineGueltig { get; private set; }

    /// <summary>
    /// Baut den Plan wie das Original beim Öffnen einer Datei auf: <c>TPlan.load</c> mit den bisherigen (Standard-)Optionen,
    /// danach <c>AssignOptions</c> mit den Optionen der Staffel. Der bestehende Spielplan wird dabei als Plan übernommen.
    /// </summary>
    public static RefPlan Laden(Staffel staffel, Berechnungsoptionen optionen)
    {
        var plan = new RefPlan(Berechnungsoptionen.Standard);
        plan.Load(staffel);
        plan.OptionenAnwenden(optionen);
        return plan;
    }

    /// <summary>Stabile Sortierung (Delphi sortiert hier unstabil; bei gleichen Schlüsseln kann die Reihenfolge abweichen).</summary>
    public static void StabilSortieren<T>(List<T> liste, Comparison<T> vergleich)
    {
        List<T> sortiert = liste.Select((e, i) => (e, i)).OrderBy(x => x, Comparer<(T E, int I)>.Create((a, b) =>
        {
            int c = vergleich(a.E, b.E);
            return c != 0 ? c : a.I.CompareTo(b.I);
        })).Select(x => x.e).ToList();
        liste.Clear();
        liste.AddRange(sortiert);
    }

    /// <summary>Original <c>cCalculateOptionsValues</c>.</summary>
    public static double Gewichtswert(Gewichtung gewichtung) => gewichtung switch
    {
        Gewichtung.NichtBeruecksichtigen => 0,
        Gewichtung.SehrWenig => 1,
        Gewichtung.Wenig => 10,
        Gewichtung.Normal => 100,
        Gewichtung.Hoch => 1000,
        Gewichtung.SehrHoch => 10000,
        _ => 100000,
    };

    public RefMannschaft? FindeNachName(string name) => Mannschaften.Find(m => m.TeamName == name);

    public RefMannschaft? FindeNachId(string id) => Mannschaften.Find(m => m.TeamId == id);

    /// <summary>Original <c>GetRoundNumberByDate</c>.</summary>
    public int RundeZuDatum(double datum) => Runden.FindIndex(r => r.Enthaelt(datum));

    /// <summary>Original <c>IsInRoundToGenerate</c>.</summary>
    public bool IstInZuPlanenderRunde(double datum)
    {
        Rundenplanung planung = Optionen.Rundenplanung;
        if (planung == Rundenplanung.Beide)
        {
            return true;
        }

        int runde = RundeZuDatum(datum);
        if (runde < 0)
        {
            return false;
        }

        int grenze = Optionen.Doppelrunde ? 1 : 0;
        return planung switch
        {
            Rundenplanung.Halbrunde or Rundenplanung.NurVorrunde => runde <= grenze,
            Rundenplanung.NurRueckrunde or Rundenplanung.Corona => runde > grenze,
            _ => true,
        };
    }

    /// <summary>Original <c>IsRoundToGenerate</c>.</summary>
    public bool IstZuPlanendeRunde(RefRunde runde) =>
        Optionen.Rundenplanung == Rundenplanung.Beide || IstInZuPlanenderRunde(runde.Von + ((runde.Bis - runde.Von) / 2));

    /// <summary>Original <c>IsFreeGameDate</c>.</summary>
    public bool IstSpielfreierTag(double datum) => SpielfreieTage.Contains(DelphiDatum.Trunc(datum));

    /// <summary>Original <c>DateIsAllowedForWeekendGames</c>.</summary>
    public bool IstWochenendeFuer60Km(double datum)
    {
        int tag = DelphiDatum.Wochentag(datum);
        return tag == 7 || tag == 1 || (tag == 6 && Optionen.FreitagZaehltZumWochenende);
    }

    /// <summary>Original <c>findGames</c>.</summary>
    public List<RefSpiel>? SpieleDerPaarung(string heimId, string gastId) =>
        spieleJePaarung.TryGetValue(heimId + "|" + gastId, out List<RefSpiel>? liste) ? liste : null;

    /// <summary>Original <c>GetMannschaftKostenFaktor</c>.</summary>
    public double MannschaftKostenFaktor(RefMannschaft mannschaft, MannschaftsKostenart art)
    {
        double ergebnis = Multiplizieren(100, Optionen.Fuer(art));
        gewichtungen.TryGetValue(mannschaft.TeamId, out MannschaftsGewichtung? eigene);
        ergebnis = Multiplizieren(ergebnis, eigene?.Gesamt ?? Gewichtung.Normal);
        return Multiplizieren(ergebnis, eigene?.JeKostenart[(int)art] ?? Gewichtung.Normal);
    }

    /// <summary>Original <c>getMidDate1</c> mit <c>getAutomaticMidDate1</c>.</summary>
    internal double Mitte1()
    {
        if (!Optionen.AutomatischeMitte)
        {
            return Optionen.Mitte1 is DateOnly m ? DelphiDatum.Wert(m) : 0;
        }

        double von = ErstesDatum();
        double bis = RueckrundenbeginnXml;
        if (Optionen.Rundenplanung == Rundenplanung.Halbrunde)
        {
            von = Beginn;
            bis = Ende;
        }

        List<double> montage = SpieltagMontage(von, bis);
        return montage.Count > 2 ? montage[montage.Count / 2] : ((bis - von) / 2) + von;
    }

    /// <summary>Original <c>getMidDate2</c> mit <c>getAutomaticMidDate2</c>.</summary>
    internal double Mitte2()
    {
        if (!Optionen.AutomatischeMitte)
        {
            return Optionen.Mitte2 is DateOnly m ? DelphiDatum.Wert(m) : 0;
        }

        double letztes = LetztesDatum();
        List<double> montage = SpieltagMontage(RueckrundenbeginnXml, letztes);
        return montage.Count > 2 ? montage[montage.Count / 2] : ((letztes - RueckrundenbeginnXml) / 2) + RueckrundenbeginnXml;
    }

    private static double Multiplizieren(double wert, Gewichtung gewichtung) =>
        gewichtung == Gewichtung.NichtBeruecksichtigen ? 0 : wert * (Gewichtswert(gewichtung) / 100.0);

    private static RefWunschtermin? WunschterminGenau(RefMannschaft mannschaft, double datum) =>
        mannschaft.Wunschtermine.Find(w => w.Datum == datum);

    /// <summary>Original <c>TGameList.SortByDate</c> (Austauschverfahren) mit <c>compGameListByDate</c>.</summary>
    private static void NachDatumSortieren(List<RefSpiel> spiele)
    {
        int i = 0;
        while (i < spiele.Count - 1)
        {
            if (Vergleichen(spiele[i], spiele[i + 1]) > 0)
            {
                (spiele[i], spiele[i + 1]) = (spiele[i + 1], spiele[i]);
                i = Math.Max(0, i - 1);
            }
            else
            {
                i++;
            }
        }
    }

    private static int Vergleichen(RefSpiel a, RefSpiel b)
    {
        if (a.Datum > b.Datum)
        {
            return 1;
        }

        if (a.Datum < b.Datum)
        {
            return -1;
        }

        return string.CompareOrdinal(a.Heim.TeamName + " " + a.Gast.TeamName, b.Heim.TeamName + " " + b.Gast.TeamName);
    }

    /// <summary>Original <c>TPlan.load</c>.</summary>
    private void Load(Staffel staffel)
    {
        Geschlecht = staffel.Geschlecht;
        RueckrundenbeginnXml = staffel.Rueckrundenbeginn is DateOnly mitte ? DelphiDatum.Wert(mitte) : 0;
        Beginn = staffel.Beginn is DateOnly von ? DelphiDatum.Wert(von) : 0;
        Ende = staffel.Ende is DateOnly bis ? DelphiDatum.Wert(bis) : 0;
        Name = staffel.Name;

        foreach (Mannschaft quelle in staffel.Mannschaften)
        {
            var mannschaft = new RefMannschaft(Mannschaften.Count);
            Mannschaften.Add(mannschaft);
            mannschaft.Laden(quelle);
        }

        StabilSortieren(Mannschaften, (a, b) => string.CompareOrdinal(a.TeamName, b.TeamName));
        for (int i = 0; i < Mannschaften.Count; i++)
        {
            Mannschaften[i].Index = i;
        }

        UngueltigeNachbarspieleEntfernen();
        VereinsteamsZuordnen();
        SpieleErzeugen();

        VorgegebeneSpiele.Clear();
        foreach (RefSpiel spiel in SpieleAus(staffel.VorgegebeneSpiele))
        {
            RefWunschtermin? wunsch = WunschterminGenau(spiel.Heim, spiel.Datum);
            var ziel = new RefSpiel(spiel.Heim, spiel.Gast);
            ziel.SetzeDatum(spiel.Datum, wunsch?.Optionen ?? default, wunsch?.ParalleleSpiele ?? 0, false, false, wunsch);
            ziel.UeberschriebenesLokal = spiel.UeberschriebenesLokal;
            VorgegebeneSpiele.Add(ziel);
        }

        foreach (DateOnly tag in staffel.SpielfreieTage)
        {
            SpielfreieTage.Add(DelphiDatum.Trunc(DelphiDatum.Wert(tag)));
        }

        foreach (Pflichtspielzeitraum zeitraum in staffel.Pflichtspielzeitraeume)
        {
            Pflichtzeiten.Add(new RefPflichtzeit(DelphiDatum.Wert(zeitraum.Von), DelphiDatum.Wert(zeitraum.Bis), zeitraum.AnzahlSpiele));
        }

        SetzlisteLaden(staffel.Setzliste);

        List<RefSpiel> bestehend = SpieleAus(staffel.BestehenderSpielplan);
        SpieleZuweisen(bestehend);
        BestehenderSpielplan.Clear();
        foreach (RefSpiel spiel in bestehend)
        {
            RefWunschtermin? wunsch = WunschterminGenau(spiel.Heim, spiel.Datum);
            var ziel = new RefSpiel(spiel.Heim, spiel.Gast);
            ziel.SetzeDatum(spiel.Datum, wunsch?.Optionen ?? default, wunsch?.ParalleleSpiele ?? 0, false, false, wunsch);
            BestehenderSpielplan.Add(ziel);
        }
    }

    /// <summary>Original <c>LoadRanking</c>.</summary>
    private void SetzlisteLaden(Setzliste? setzliste)
    {
        Setzlistenplaetze.Clear();
        if (setzliste is null || !setzliste.Aktiv)
        {
            return;
        }

        foreach (Setzlisteneintrag eintrag in setzliste.Eintraege)
        {
            RefMannschaft? mannschaft = FindeNachName(eintrag.Mannschaft);
            if (mannschaft is not null)
            {
                while (Setzlistenplaetze.Count <= eintrag.Platz)
                {
                    Setzlistenplaetze.Add(-1);
                }

                Setzlistenplaetze[eintrag.Platz] = mannschaft.Index;
            }
        }

        Setzlistenplaetze.RemoveAll(i => i < 0);
        foreach (RefMannschaft mannschaft in Mannschaften.Where(m => !Setzlistenplaetze.Contains(m.Index)))
        {
            Setzlistenplaetze.Add(mannschaft.Index);
        }
    }

    /// <summary>Original <c>LoadGameDates</c>: Spiele mit unbekannten Mannschaften entfallen.</summary>
    private List<RefSpiel> SpieleAus(IEnumerable<Spiel> spiele)
    {
        var ergebnis = new List<RefSpiel>();
        foreach (Spiel spiel in spiele)
        {
            RefMannschaft? heim = FindeNachName(spiel.Heim);
            RefMannschaft? gast = FindeNachName(spiel.Gast);
            if (heim is null || gast is null)
            {
                continue;
            }

            var neu = new RefSpiel(heim, gast);
            neu.SetzeDatum(spiel.Zeitpunkt is DateTime z ? DelphiDatum.Wert(z) : 0, default, 0, false, false, null);
            neu.UeberschriebenesLokal = spiel.Spiellokal;
            ergebnis.Add(neu);
        }

        return ergebnis;
    }

    /// <summary>Original <c>AssignOptions</c>.</summary>
    private void OptionenAnwenden(Berechnungsoptionen optionen)
    {
        List<(double Datum, string Heim, string Gast)> bisher = SpielplanAuslesen();
        Optionen = optionen;
        gewichtungen.Clear();
        foreach (MannschaftsGewichtung g in optionen.Mannschaften)
        {
            gewichtungen[g.MannschaftsId] = g;
        }

        TermineAusOptionenAnpassen();
        SpielplanZuweisen(bisher);
        NichtNotwendigeMarkieren();
    }

    /// <summary>Original <c>getSchedule</c> (ohne Spiellokale).</summary>
    private List<(double Datum, string Heim, string Gast)> SpielplanAuslesen() =>
        SpieleErlaubt.Concat(SpieleNichtErlaubt).Select(s => (s.Datum, s.Heim.TeamName, s.Gast.TeamName)).ToList();

    /// <summary>Original <c>ModifyTermineFromOptions</c>.</summary>
    private void TermineAusOptionenAnpassen()
    {
        foreach (RefMannschaft mannschaft in Mannschaften)
        {
            mannschaft.Sperrtermine.RemoveAll(d => !IstGueltigFuerWunschtermin(d));
            mannschaft.Wunschtermine.RemoveAll(w =>
                Terminoptionen.Hat(w.Optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit)
                || Terminoptionen.Hat(w.Optionen, Wunschterminoptionen.KoppelZweitzeit)
                || !IstGueltigFuerWunschtermin(w.Datum));
        }

        ZusatztermineAnlegen(w => Terminoptionen.IstKoppelAmTag(w.Optionen), Wunschterminoptionen.KoppelZweitzeit);
        ZusatztermineAnlegen(w => Terminoptionen.Hat(w.Optionen, Wunschterminoptionen.AuswaertsKoppelHatZweitzeit), Wunschterminoptionen.AuswaertsKoppelZweitzeit);
    }

    private void ZusatztermineAnlegen(Func<RefWunschtermin, bool> bedingung, Wunschterminoptionen zusatz)
    {
        foreach (List<RefWunschtermin> termine in Mannschaften.Select(m => m.Wunschtermine))
        {
            var neu = termine.Where(bedingung).Select(w =>
            {
                RefWunschtermin kopie = w.Kopie();
                kopie.Optionen |= zusatz;
                kopie.Datum = w.KoppelZweitzeit;
                return kopie;
            }).ToList();

            if (neu.Count > 0)
            {
                termine.AddRange(neu);
                StabilSortieren(termine, (a, b) => a.Datum.CompareTo(b.Datum));
            }
        }
    }

    /// <summary>Original <c>isValidDateForWunschtermin</c>.</summary>
    private bool IstGueltigFuerWunschtermin(double datum) =>
        Optionen.Rundenplanung != Rundenplanung.Halbrunde || (datum >= Beginn && datum <= Ende);

    /// <summary>Original <c>RemoveNotValidSisterGames</c>.</summary>
    private void UngueltigeNachbarspieleEntfernen()
    {
        foreach (List<RefNachbarspiel> liste in Mannschaften.SelectMany(m => m.Nachbarspiele.Values))
        {
            liste.RemoveAll(s => s.Geschlecht == Geschlecht && (FindeNachName(s.Heim) is not null || FindeNachName(s.Gast) is not null));
        }

        foreach (List<RefNachbarspiel> liste in Mannschaften.SelectMany(m => m.Nachbarspiele.Values))
        {
            for (int i = liste.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (liste[i].Heim == liste[j].Heim && liste[i].Gast == liste[j].Gast)
                    {
                        liste.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>Original <c>AddSisterTeamsInPlan</c>.</summary>
    private void VereinsteamsZuordnen()
    {
        foreach (RefMannschaft m1 in Mannschaften)
        {
            m1.VereinsteamsImPlan.Clear();
            m1.VereinsteamsImPlan.AddRange(Mannschaften.Where(m2 => !ReferenceEquals(m1, m2) && m1.ClubId == m2.ClubId));
        }
    }

    /// <summary>Original <c>GenerateGames</c>.</summary>
    private void SpieleErzeugen()
    {
        RundenErzeugen();
        SpieleErlaubt.Clear();
        SpieleNichtErlaubt.Clear();
        foreach (RefMannschaft heim in Mannschaften)
        {
            foreach (RefMannschaft gast in Mannschaften.Where(g => !ReferenceEquals(g, heim)))
            {
                SpieleErlaubt.Add(new RefSpiel(heim, gast));
                if (Optionen.Doppelrunde)
                {
                    SpieleErlaubt.Add(new RefSpiel(heim, gast));
                }
            }
        }

        spieleJePaarung.Clear();
        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            string schluessel = spiel.Heim.TeamId + "|" + spiel.Gast.TeamId;
            if (!spieleJePaarung.TryGetValue(schluessel, out List<RefSpiel>? liste))
            {
                liste = [];
                spieleJePaarung.Add(schluessel, liste);
            }

            liste.Add(spiel);
        }

        SpiellistenAufbauen();
    }

    /// <summary>Original <c>GenerateMannschaftsRefGames</c> mit <c>SortMannschaftsDates</c>.</summary>
    private void SpiellistenAufbauen()
    {
        foreach (RefMannschaft mannschaft in Mannschaften)
        {
            mannschaft.Spiele.Clear();
        }

        foreach (RefSpiel spiel in SpieleErlaubt.Concat(SpieleNichtErlaubt))
        {
            spiel.Heim.Spiele.Add(spiel);
            spiel.Gast.Spiele.Add(spiel);
        }

        foreach (RefMannschaft mannschaft in Mannschaften)
        {
            NachDatumSortieren(mannschaft.Spiele);
        }
    }

    /// <summary>Original <c>CreateRoundInfo</c>.</summary>
    private void RundenErzeugen()
    {
        Runden.Clear();
        if (Optionen.Rundenplanung == Rundenplanung.Halbrunde)
        {
            if (Optionen.Doppelrunde)
            {
                double mitte1 = Mitte1();
                double mitte2 = Mitte2();
                if (mitte1 < Beginn)
                {
                    mitte1 = mitte2;
                }

                Runden.Add(new RefRunde(Beginn, mitte1));
                Runden.Add(new RefRunde(mitte1, Ende));
            }
            else
            {
                Runden.Add(new RefRunde(Beginn, Ende));
            }
        }
        else if (Optionen.Doppelrunde)
        {
            double mitte1 = Mitte1();
            double mitte2 = Mitte2();
            Runden.Add(new RefRunde(ErstesDatum() - 100, mitte1));
            Runden.Add(new RefRunde(mitte1, RueckrundenbeginnXml));
            Runden.Add(new RefRunde(RueckrundenbeginnXml, mitte2));
            Runden.Add(new RefRunde(mitte2, LetztesDatum() + 100));
        }
        else
        {
            Runden.Add(new RefRunde(ErstesDatum() - 100, RueckrundenbeginnXml));
            Runden.Add(new RefRunde(RueckrundenbeginnXml, LetztesDatum() + 100));
        }
    }

    /// <summary>Original <c>getSpieltagMondays</c> (nur Wunschtermine).</summary>
    private List<double> SpieltagMontage(double von, double bis)
    {
        var montage = new List<double>();
        foreach (RefWunschtermin w in Mannschaften.SelectMany(m => m.Wunschtermine).Where(w => w.Datum >= von && w.Datum <= bis))
        {
            double montag = DelphiDatum.MontagDavor(w.Datum);
            if (!montage.Contains(montag))
            {
                montage.Add(montag);
            }
        }

        montage.Sort();
        return montage;
    }

    /// <summary>Original <c>GetFirstDate</c>.</summary>
    private double ErstesDatum()
    {
        List<double> daten = Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => w.Datum).Where(d => d != 0.0).ToList();
        return daten.Count == 0 ? 0.0 : daten.Min();
    }

    /// <summary>Original <c>GetLastDate</c>.</summary>
    private double LetztesDatum()
    {
        List<double> daten = Mannschaften.SelectMany(m => m.Wunschtermine).Select(w => w.Datum).Where(d => d != 0.0).ToList();
        return daten.Count == 0 ? 0.0 : daten.Max();
    }

    /// <summary>Original <c>AssignGames</c> (Zuordnung über die Mannschafts-Id).</summary>
    private void SpieleZuweisen(List<RefSpiel> quelle)
    {
        AlleTermineLoeschen();
        SpieleErzeugen();
        SpieleNichtErlaubt.Clear();
        foreach (RefSpiel spiel in quelle)
        {
            Uebernehmen(spiel.Datum, spiel.Heim.TeamId, spiel.Gast.TeamId, m => m.TeamId, spiel.NichtNotwendig);
        }

        SpiellistenAufbauen();
        AlleTermineGueltig = false;
    }

    /// <summary>Original <c>AssignSchedule</c> (Zuordnung über den Mannschaftsnamen).</summary>
    private void SpielplanZuweisen(List<(double Datum, string Heim, string Gast)> quelle)
    {
        AlleTermineLoeschen();
        SpieleErzeugen();
        SpieleNichtErlaubt.Clear();
        foreach ((double datum, string heim, string gast) in quelle)
        {
            Uebernehmen(datum, heim, gast, m => m.TeamName, false);
        }

        SpiellistenAufbauen();
        NichtNotwendigeMarkieren();
        AlleTermineGueltig = false;
    }

    private void Uebernehmen(double datum, string heimSchluessel, string gastSchluessel, Func<RefMannschaft, string> schluessel, bool nichtNotwendig)
    {
        RefSpiel? ziel = SpieleErlaubt.Find(s => !s.DatumGueltig && schluessel(s.Heim) == heimSchluessel && schluessel(s.Gast) == gastSchluessel);
        if (ziel is not null)
        {
            Wunschterminoptionen koppel = default;
            int maxHeim = 0;
            foreach (RefWunschtermin w in ziel.Heim.Wunschtermine.Where(w => DelphiDatum.Trunc(w.Datum) == DelphiDatum.Trunc(datum)))
            {
                koppel = w.Optionen;
                maxHeim = w.ParalleleSpiele;
            }

            ziel.SetzeDatum(datum, koppel, maxHeim, false, nichtNotwendig, WunschterminGenau(ziel.Heim, datum));
            return;
        }

        RefMannschaft? gast = Mannschaften.Find(m => schluessel(m) == gastSchluessel);
        RefMannschaft? heimM = Mannschaften.Find(m => schluessel(m) == heimSchluessel);
        if (gast is not null && heimM is not null)
        {
            var ungueltig = new RefSpiel(heimM, gast);
            ungueltig.SetzeDatum(datum, default, 0, false, false, null);
            SpieleNichtErlaubt.Add(ungueltig);
        }
    }

    /// <summary>Original <c>clearAllDates</c>.</summary>
    private void AlleTermineLoeschen()
    {
        foreach (RefWunschtermin w in Mannschaften.SelectMany(m => m.Wunschtermine))
        {
            w.ZugeordnetesSpiel = null;
        }

        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            spiel.AlleWerteLoeschen();
        }

        SpieleNichtErlaubt.Clear();
        SpiellistenAufbauen();
    }

    /// <summary>Original <c>MarkNotNecessaryGames</c>.</summary>
    private void NichtNotwendigeMarkieren()
    {
        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            spiel.SetzeDatum(spiel.Datum, spiel.DatumOptionen, spiel.DatumMaxHeim, spiel.FestesDatum, false, spiel.Wunschtermin);
        }

        Rundenplanung planung = Optionen.Rundenplanung;
        if (planung == Rundenplanung.Beide)
        {
            return;
        }

        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            bool markieren = planung == Rundenplanung.Corona ? spiel.DatumGueltig : IstInZuPlanenderRunde(spiel.Datum);
            if (markieren)
            {
                RueckspielNichtNotwendig(spiel);
            }
        }
    }

    /// <summary>Original <c>MarkSecondGameAsUnNecessary</c>.</summary>
    private void RueckspielNichtNotwendig(RefSpiel spiel)
    {
        List<RefSpiel>? spiele = SpieleDerPaarung(spiel.Gast.TeamId, spiel.Heim.TeamId);
        RefSpiel? rueckspiel = spiele?.Find(s => !s.NichtNotwendig && !s.DatumGueltig);
        rueckspiel?.SetzeDatum(rueckspiel.Datum, default, 0, false, true, null);
    }
}
