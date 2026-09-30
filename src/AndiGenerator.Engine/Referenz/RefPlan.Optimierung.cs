// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Optimierungsschritte des Originals (<c>FillTermine</c>, <c>NeuWuerfeln</c>, <c>GenerateRaster</c>, <c>FillPredefinedGames</c>,
/// <c>TransferDateToRefDate</c>/<c>TransferRefDateToDate</c>, <c>RemoveNotValidDates</c>).
/// </summary>
internal sealed partial class RefPlan
{
    /// <summary>Original <c>RemoveNotValidDates</c>.</summary>
    public void UngueltigeTermineEntfernen()
    {
        foreach (RefWunschtermin w in Mannschaften.SelectMany(m => m.Wunschtermine))
        {
            w.ZugeordnetesSpiel = null;
        }

        SpieleNichtErlaubt.Clear();
        SpiellistenAufbauen();
        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            if (!SpielIstGueltig(spiel))
            {
                spiel.AlleWerteLoeschen();
            }
            else if (spiel.Wunschtermin is not null)
            {
                spiel.Wunschtermin.ZugeordnetesSpiel = spiel;
            }
        }

        NichtNotwendigeMarkieren();
        AlleTermineGueltig = true;
    }

    /// <summary>Original <c>clearAllDates</c>.</summary>
    public void TermineLeeren() => AlleTermineLoeschen();

    /// <summary>Original <c>TransferDateToRefDate</c>.</summary>
    public void Merken()
    {
        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            spiel.Merken();
        }
    }

    /// <summary>Original <c>TransferRefDateToDate</c> (die Spiellisten der Mannschaften werden wie im Original nicht neu sortiert).</summary>
    public void Zuruecksetzen()
    {
        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            spiel.Zuruecksetzen();
        }
    }

    /// <summary>Original <c>FillPredefinedGames</c>.</summary>
    public void VorgegebeneSpieleSetzen()
    {
        foreach (RefSpiel vorgabe in VorgegebeneSpiele)
        {
            VorgegebenesSpielSetzen(vorgabe);
        }

        if (Optionen.Rundenplanung is Rundenplanung.NurRueckrunde or Rundenplanung.Corona)
        {
            foreach (RefSpiel vorgabe in BestehenderSpielplan.Where(s => !IstInZuPlanenderRunde(s.Datum)))
            {
                VorgegebenesSpielSetzen(vorgabe);
            }
        }
    }

    /// <summary>Original <c>NeuWuerfeln</c>.</summary>
    public void NeuWuerfeln(Tauschstrategie strategie, Zufall zufall)
    {
        switch (strategie.Typ)
        {
            case Tauschtyp.Raster:
                RasterErzeugen(zufall);
                break;
            case Tauschtyp.Mannschaft when Mannschaften.Count > 0:
                {
                    var spiele = new List<RefSpiel>();
                    for (int i = 1; i <= strategie.Anzahl; i++)
                    {
                        int j = Math.Min(Mannschaften.Count - 1, zufall.Zahl(Mannschaften.Count));
                        spiele.AddRange(Mannschaften[j].Spiele);
                    }

                    NeuWuerfelnFuerSpiele(spiele, strategie.Prozent, false, zufall);
                    break;
                }

            case Tauschtyp.Spieltag when Mannschaften.Count > 0:
                {
                    int spieltage = Mannschaften[0].Spiele.Count;
                    var spiele = new List<RefSpiel>();
                    for (int i = 1; i <= strategie.Anzahl; i++)
                    {
                        int j = Math.Min(spieltage - 1, zufall.Zahl(spieltage));
                        spiele.AddRange(Mannschaften.Where(m => j < m.Spiele.Count).Select(m => m.Spiele[j]));
                    }

                    NeuWuerfelnFuerSpiele(spiele, strategie.Prozent, true, zufall);
                    break;
                }

            case Tauschtyp.Normal:
                NeuWuerfelnFuerSpiele(SpieleErlaubt, strategie.Prozent, true, zufall);
                break;
            default:
                break;
        }
    }

    /// <summary>Original <c>FillTermine</c>.</summary>
    public void TermineFuellen(Zufall zufall)
    {
        TermineFuellenIntern(true, zufall);
        if (HatAuswaertsKoppeltermine() || HatKoppeltermine())
        {
            UngueltigeZweitterminSpieleEntfernen();
            TermineFuellenIntern(false, zufall);
        }
    }

    /// <summary>Original <c>GenerateRaster</c>.</summary>
    public void RasterErzeugen(Zufall zufall)
    {
        if (Mannschaften.Count < 2)
        {
            return;
        }

        foreach (RefSpiel spiel in SpieleErlaubt.Where(s => !s.FestesDatum))
        {
            spiel.DatumLeeren();
        }

        if (Optionen.Rundenplanung != Rundenplanung.Beide)
        {
            NichtNotwendigeMarkieren();
        }

        var mannschaften = new List<RefMannschaft>(Mannschaften);
        for (int i = 0; i <= 100; i++)
        {
            int i1 = zufall.Zahl(mannschaften.Count);
            int i2 = zufall.Zahl(mannschaften.Count);
            if (i2 != i1)
            {
                (mannschaften[i1], mannschaften[i2]) = (mannschaften[i2], mannschaften[i1]);
            }
        }

        var spieltagIndexe = Enumerable.Range(0, mannschaften.Count - 1).ToList();
        for (int i = 0; i <= 100; i++)
        {
            int i1 = zufall.Zahl(spieltagIndexe.Count);
            int i2 = zufall.Zahl(spieltagIndexe.Count);
            if (i2 != i1)
            {
                (spieltagIndexe[i1], spieltagIndexe[i2]) = (spieltagIndexe[i2], spieltagIndexe[i1]);
            }
        }

        for (int i = 0; i < Runden.Count; i++)
        {
            RasterEinerRunde(mannschaften, spieltagIndexe, Runden[i], i % 2 == 0, zufall);
        }
    }

    /// <summary>Original <c>ClearGameList</c>.</summary>
    private static void SpieleLeeren(List<RefSpiel>? spiele)
    {
        foreach (RefSpiel spiel in spiele?.Where(s => !s.FestesDatum) ?? [])
        {
            spiel.DatumLeeren();
        }
    }

    /// <summary>Original <c>FillPredefinedGame</c>.</summary>
    private void VorgegebenesSpielSetzen(RefSpiel vorgabe)
    {
        List<RefSpiel>? spiele = SpieleDerPaarung(vorgabe.Heim.TeamId, vorgabe.Gast.TeamId);
        if (spiele is null || spiele.Count == 0)
        {
            return;
        }

        RefSpiel? gefunden = spiele.FindLast(s => s.Datum == vorgabe.Datum);
        if (gefunden is null)
        {
            gefunden = spiele[0];
            gefunden = spiele.FindLast(s => !s.DatumGueltig) ?? gefunden;
        }

        RefWunschtermin? wunsch = gefunden.Heim.Wunschtermine.FindLast(w => w.Datum == vorgabe.Datum);
        gefunden.SetzeDatum(vorgabe.Datum, wunsch?.Optionen ?? default, wunsch?.ParalleleSpiele ?? 0, true, false, wunsch);
    }

    /// <summary>Original <c>NeuWuerfelnForGames</c>.</summary>
    private void NeuWuerfelnFuerSpiele(List<RefSpiel> spiele, double prozent, bool mitRueckspiel, Zufall zufall)
    {
        if (prozent == 0)
        {
            return;
        }

        if (prozent == 100)
        {
            SpieleLeeren(spiele);
            return;
        }

        var offen = new List<RefSpiel>(spiele);
        int ziel = Math.Max(1, Math.Min(spiele.Count, DelphiDatum.Trunc(spiele.Count * prozent / 100.0)));
        int entfernt = 0;
        while (entfernt < ziel && offen.Count > 0)
        {
            int i = Math.Min(offen.Count - 1, zufall.Zahl(offen.Count));
            RefSpiel spiel = offen[i];
            if (spiel.Datum == 0.0)
            {
                offen.RemoveAt(i);
                continue;
            }

            if (mitRueckspiel && (HatKoppeltermine() || HatAuswaertsKoppeltermine()))
            {
                RefSpiel? koppel = spiel.Heim.GekoppeltesSpiel(spiel.Heim.Spiele.IndexOf(spiel));
                if (koppel is not null && !koppel.FestesDatum)
                {
                    koppel.DatumLeeren();
                }

                if (spiel.Gast.Auswaertskoppeln.Count > 0)
                {
                    SpieleLeeren(spiel.Gast.AuswaertskoppelAbhaengigkeiten(this, spiel.Heim));
                }
            }

            if (!spiel.FestesDatum)
            {
                spiel.DatumLeeren();
            }

            offen.RemoveAt(i);
            entfernt++;
            if (mitRueckspiel)
            {
                SpieleLeeren(SpieleDerPaarung(spiel.Heim.TeamId, spiel.Gast.TeamId));
                SpieleLeeren(SpieleDerPaarung(spiel.Gast.TeamId, spiel.Heim.TeamId));
            }
        }
    }

    /// <summary>Original <c>FillTermineIntern</c>.</summary>
    private void TermineFuellenIntern(bool alleZweitzeitenGueltig, Zufall zufall)
    {
        if (Optionen.Rundenplanung != Rundenplanung.Beide)
        {
            NichtNotwendigeMarkieren();
        }

        List<RefSpiel> offen = SpieleErlaubt.Where(s => s.Datum == 0.0).ToList();
        while (offen.Count > 0)
        {
            int i = zufall.Zahl(offen.Count);
            RefSpiel spiel = offen[i];
            if (spiel.NichtNotwendig)
            {
                offen.RemoveAt(i);
                continue;
            }

            TerminFuellen(spiel, Enumerable.Range(0, spiel.Heim.Wunschtermine.Count).ToList(), alleZweitzeitenGueltig, zufall);

            if (HatAuswaertsKoppeltermine() && zufall.Prozent(70))
            {
                List<RefSpiel>? abhaengig = spiel.Gast.AuswaertskoppelAbhaengigkeiten(this, spiel.Heim);
                if (abhaengig is { Count: > 0 })
                {
                    RefSpiel zweites = abhaengig[zufall.Zahl(abhaengig.Count)];
                    List<int> indexe = Enumerable.Range(0, zweites.Heim.Wunschtermine.Count)
                        .Where(k => Math.Abs(DelphiDatum.Trunc(spiel.Datum) - DelphiDatum.Trunc(zweites.Heim.Wunschtermine[k].Datum)) <= 1)
                        .ToList();
                    if (indexe.Count > 0)
                    {
                        TerminFuellen(zweites, indexe, alleZweitzeitenGueltig, zufall);
                    }
                }
            }

            offen.RemoveAt(i);
        }

        foreach (RefMannschaft mannschaft in Mannschaften)
        {
            NachDatumSortieren(mannschaft.Spiele);
        }
    }

    /// <summary>Original <c>FillTerminIntern</c>.</summary>
    private void TerminFuellen(RefSpiel spiel, List<int> indexe, bool alleZweitzeitenGueltig, Zufall zufall)
    {
        while (indexe.Count > 0)
        {
            int i = zufall.Zahl(indexe.Count);
            RefWunschtermin wunsch = spiel.Heim.Wunschtermine[indexe[i]];
            bool gueltig = wunsch.ZugeordnetesSpiel is null && IstInZuPlanenderRunde(wunsch.Datum);
            if (gueltig
                && Terminoptionen.Hat(wunsch.Optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit)
                && !Terminoptionen.Hat(wunsch.Optionen, Wunschterminoptionen.KoppelZweitzeit)
                && !spiel.Gast.HatAuswaertskoppelAmSelbenTagMit(this, spiel.Heim))
            {
                gueltig = false;
            }

            if (gueltig && TerminIstGueltig(wunsch, spiel, null, alleZweitzeitenGueltig))
            {
                spiel.SetzeDatum(wunsch.Datum, wunsch.Optionen, wunsch.ParalleleSpiele, false, false, wunsch);
                if (Optionen.Rundenplanung != Rundenplanung.Beide)
                {
                    RueckspielNichtNotwendig(spiel);
                }

                return;
            }

            indexe.RemoveAt(i);
        }
    }

    /// <summary>Original <c>RemoveNotValidSecondTimeGames</c>.</summary>
    private void UngueltigeZweitterminSpieleEntfernen()
    {
        foreach (RefSpiel spiel in SpieleErlaubt)
        {
            if ((Terminoptionen.Hat(spiel.DatumOptionen, Wunschterminoptionen.KoppelZweitzeit)
                    || Terminoptionen.Hat(spiel.DatumOptionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit))
                && !SpielIstGueltig(spiel))
            {
                spiel.DatumLeeren();
            }
        }
    }

    /// <summary>Original <c>CreateRasterOneRound</c>.</summary>
    private void RasterEinerRunde(List<RefMannschaft> mannschaften, List<int> spieltagIndexe, RefRunde runde, bool rueckrunde, Zufall zufall)
    {
        List<double> montage = SpieltagMontage(runde.Von, runde.Bis);
        while (montage.Count > mannschaften.Count - 1)
        {
            montage.RemoveAt(zufall.Zahl(montage.Count));
        }

        RefMannschaft? platzhalter = null;
        int anzahl = mannschaften.Count;
        if (mannschaften.Count % 2 == 0)
        {
            platzhalter = mannschaften[^1];
            anzahl--;
        }

        for (int m = 0; m < anzahl; m++)
        {
            for (int spieltag = 0; spieltag < montage.Count; spieltag++)
            {
                int m2 = 0 - spieltagIndexe[spieltag] - m;
                while (m2 < 0)
                {
                    m2 += anzahl;
                }

                if (m2 < m)
                {
                    continue;
                }

                RefMannschaft? gegner = m2 == m ? platzhalter : mannschaften[m2];
                if (gegner is null)
                {
                    continue;
                }

                bool rueckspiel = rueckrunde;
                if (spieltag % 2 == 0)
                {
                    rueckspiel = !rueckspiel;
                }

                if (spieltag >= montage.Count / 2.0)
                {
                    rueckspiel = !rueckspiel;
                }

                if (rueckspiel)
                {
                    SpielFuerRaster(mannschaften[m], gegner, montage[spieltag]);
                }
                else
                {
                    SpielFuerRaster(gegner, mannschaften[m], montage[spieltag]);
                }
            }
        }
    }

    /// <summary>Original <c>ScheduleGameForRaster</c>.</summary>
    private void SpielFuerRaster(RefMannschaft heim, RefMannschaft gast, double montag)
    {
        foreach (RefWunschtermin wunsch in heim.Wunschtermine.Where(w => DelphiDatum.MontagDavor(w.Datum) == montag))
        {
            List<RefSpiel>? spiele = SpieleDerPaarung(heim.TeamId, gast.TeamId);
            RefSpiel? spiel = spiele?.Find(s => !s.NichtNotwendig && !s.DatumGueltig && IstInZuPlanenderRunde(wunsch.Datum)
                && TerminIstGueltig(wunsch, s, null, false));
            if (spiel is not null)
            {
                spiel.SetzeDatum(wunsch.Datum, wunsch.Optionen, wunsch.ParalleleSpiele, false, false, wunsch);
                if (Optionen.Rundenplanung != Rundenplanung.Beide)
                {
                    RueckspielNichtNotwendig(spiel);
                }
            }
        }
    }
}
