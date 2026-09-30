// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Optimierungsschritte des Originals auf Arrays (<c>FillTermine</c>, <c>NeuWuerfeln</c>, <c>GenerateRaster</c>,
/// <c>FillPredefinedGames</c>, Merken/Zurücksetzen, <c>RemoveNotValidDates</c>). Zufallszahlen werden in genau derselben
/// Reihenfolge und mit denselben Bereichen gezogen wie im Referenzmodell.
/// </summary>
internal sealed partial class KernPlan
{
    /// <summary>Original <c>TransferDateToRefDate</c>.</summary>
    public void Merken()
    {
        Array.Copy(Datum, gemerktDatum, anzahlErlaubt);
        Array.Copy(Opt, gemerktOptionen, anzahlErlaubt);
        Array.Copy(MaxHeim, gemerktMaxHeim, anzahlErlaubt);
        Array.Copy(Wunsch, gemerktWunsch, anzahlErlaubt);
        Array.Copy(NichtNotwendig, gemerktNichtNotwendig, anzahlErlaubt);
    }

    /// <summary>Original <c>TransferRefDateToDate</c> (ohne Neusortierung der Spiellisten, wie im Original).</summary>
    public void Zuruecksetzen()
    {
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if (!Fest[s])
            {
                SetzeDatum(s, gemerktDatum[s], gemerktOptionen[s], gemerktMaxHeim[s], false, gemerktNichtNotwendig[s], gemerktWunsch[s]);
            }
        }
    }

    /// <summary>Original <c>clearAllDates</c>.</summary>
    public void TermineLeeren()
    {
        Array.Fill(WunschSpiel, -1);
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            AlleWerteLoeschen(s);
        }

        anzahlAktiv = anzahlErlaubt;
        SpiellistenAufbauen();
    }

    /// <summary>Original <c>RemoveNotValidDates</c>.</summary>
    public void UngueltigeTermineEntfernen()
    {
        Array.Fill(WunschSpiel, -1);
        anzahlAktiv = anzahlErlaubt;
        SpiellistenAufbauen();
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if (!SpielIstGueltig(s))
            {
                AlleWerteLoeschen(s);
            }
            else if (Wunsch[s] >= 0)
            {
                WunschSpiel[Wunsch[s]] = s;
            }
        }

        NichtNotwendigeMarkieren();
        AlleTermineGueltig = true;
    }

    /// <summary>Original <c>FillPredefinedGames</c>.</summary>
    public void VorgegebeneSpieleSetzen()
    {
        foreach (FesterTermin vorgabe in D.Vorgegeben)
        {
            VorgegebenesSpielSetzen(vorgabe);
        }

        if (D.Rundenplanung is Rundenplanung.NurRueckrunde or Rundenplanung.Corona)
        {
            foreach (FesterTermin vorgabe in D.Bestehend.Where(v => !D.IstInZuPlanenderRunde(v.Datum)))
            {
                VorgegebenesSpielSetzen(vorgabe);
            }
        }
    }

    /// <summary>Original <c>NeuWuerfeln</c>.</summary>
    public void NeuWuerfeln(Tauschstrategie strategie, Zufall zufall)
    {
        int anzahl = 0;
        switch (strategie.Typ)
        {
            case Tauschtyp.Raster:
                RasterErzeugen(zufall);
                break;
            case Tauschtyp.Mannschaft when D.N > 0:
                for (int i = 1; i <= strategie.Anzahl; i++)
                {
                    int j = Math.Min(D.N - 1, zufall.Zahl(D.N));
                    Array.Copy(Spiele[j], 0, auswahl, anzahl, SpieleAnzahl[j]);
                    anzahl += SpieleAnzahl[j];
                }

                NeuWuerfelnFuerSpiele(anzahl, strategie.Prozent, false, zufall);
                break;
            case Tauschtyp.Spieltag when D.N > 0:
                int spieltage = SpieleAnzahl[0];
                for (int i = 1; i <= strategie.Anzahl; i++)
                {
                    int j = Math.Min(spieltage - 1, zufall.Zahl(spieltage));
                    for (int t = 0; t < D.N; t++)
                    {
                        if (j < SpieleAnzahl[t])
                        {
                            auswahl[anzahl++] = Spiele[t][j];
                        }
                    }
                }

                NeuWuerfelnFuerSpiele(anzahl, strategie.Prozent, true, zufall);
                break;
            case Tauschtyp.Normal:
                for (int s = 0; s < anzahlErlaubt; s++)
                {
                    auswahl[s] = s;
                }

                NeuWuerfelnFuerSpiele(anzahlErlaubt, strategie.Prozent, true, zufall);
                break;
            default:
                break;
        }
    }

    /// <summary>Original <c>FillTermine</c>.</summary>
    public void TermineFuellen(Zufall zufall)
    {
        TermineFuellenIntern(true, zufall);
        if (D.HatAuswaertsKoppeltermine || D.HatKoppeltermine)
        {
            UngueltigeZweitterminSpieleEntfernen();
            TermineFuellenIntern(false, zufall);
        }
    }

    /// <summary>Original <c>GenerateRaster</c>.</summary>
    public void RasterErzeugen(Zufall zufall)
    {
        int n = D.N;
        if (n < 2)
        {
            return;
        }

        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if (!Fest[s])
            {
                DatumLeeren(s);
            }
        }

        if (D.Rundenplanung != Rundenplanung.Beide)
        {
            NichtNotwendigeMarkieren();
        }

        for (int i = 0; i < n; i++)
        {
            reihenfolge[i] = i;
        }

        Mischen(reihenfolge, n, zufall);
        for (int i = 0; i < n - 1; i++)
        {
            spieltagIndexe[i] = i;
        }

        Mischen(spieltagIndexe, n - 1, zufall);
        for (int i = 0; i < D.Runden.Length; i++)
        {
            RasterEinerRunde(i, i % 2 == 0, zufall);
        }
    }

    /// <summary>101 zufällige Vertauschungen wie in <c>GenerateRaster</c>.</summary>
    private static void Mischen(int[] werte, int anzahl, Zufall zufall)
    {
        for (int i = 0; i <= 100; i++)
        {
            int i1 = zufall.Zahl(anzahl);
            int i2 = zufall.Zahl(anzahl);
            if (i2 != i1)
            {
                (werte[i1], werte[i2]) = (werte[i2], werte[i1]);
            }
        }
    }

    /// <summary>Entfernt den Eintrag an <paramref name="index"/> unter Erhalt der Reihenfolge (wie <c>List.RemoveAt</c>).</summary>
    private static int Entfernen(int[] liste, int anzahl, int index)
    {
        Array.Copy(liste, index + 1, liste, index, anzahl - index - 1);
        return anzahl - 1;
    }

    /// <summary>Original <c>FillPredefinedGame</c>.</summary>
    private void VorgegebenesSpielSetzen(FesterTermin vorgabe)
    {
        int[] spiele = D.Paarung[(vorgabe.Heim * D.N) + vorgabe.Gast];
        if (spiele.Length == 0)
        {
            return;
        }

        int gefunden = -1;
        for (int i = spiele.Length - 1; i >= 0 && gefunden < 0; i--)
        {
            if (Datum[spiele[i]] == vorgabe.Datum)
            {
                gefunden = spiele[i];
            }
        }

        if (gefunden < 0)
        {
            gefunden = spiele[0];
            for (int i = spiele.Length - 1; i >= 0; i--)
            {
                if (Datum[spiele[i]] == 0.0)
                {
                    gefunden = spiele[i];
                    break;
                }
            }
        }

        int heim = Heim[gefunden];
        int wunsch = -1;
        for (int w = D.WunschStart[heim + 1] - 1; w >= D.WunschStart[heim]; w--)
        {
            if (D.WunschDatum[w] == vorgabe.Datum)
            {
                wunsch = w;
                break;
            }
        }

        SetzeDatum(
            gefunden,
            vorgabe.Datum,
            wunsch >= 0 ? D.WunschOptionen[wunsch] : default,
            wunsch >= 0 ? D.WunschParallele[wunsch] : 0,
            true,
            false,
            wunsch);
    }

    /// <summary>Original <c>NeuWuerfelnForGames</c> für die ersten <paramref name="anzahl"/> Spiele in <c>auswahl</c>.</summary>
    private void NeuWuerfelnFuerSpiele(int anzahl, double prozent, bool mitRueckspiel, Zufall zufall)
    {
        if (prozent == 0)
        {
            return;
        }

        if (prozent == 100)
        {
            SpieleLeeren(auswahl.AsSpan(0, anzahl));
            return;
        }

        Array.Copy(auswahl, offen, anzahl);
        int anzahlOffen = anzahl;
        int ziel = Math.Max(1, Math.Min(anzahl, DelphiDatum.Trunc(anzahl * prozent / 100.0)));
        int entfernt = 0;
        bool koppeln = D.HatKoppeltermine || D.HatAuswaertsKoppeltermine;
        while (entfernt < ziel && anzahlOffen > 0)
        {
            int i = Math.Min(anzahlOffen - 1, zufall.Zahl(anzahlOffen));
            int s = offen[i];
            if (Datum[s] == 0.0)
            {
                anzahlOffen = Entfernen(offen, anzahlOffen, i);
                continue;
            }

            int heim = Heim[s];
            int gast = Gast[s];
            if (mitRueckspiel && koppeln)
            {
                int koppel = Gekoppelt(heim, Array.IndexOf(Spiele[heim], s, 0, SpieleAnzahl[heim]));
                if (koppel >= 0 && !Fest[koppel])
                {
                    DatumLeeren(koppel);
                }

                if (D.Auswaertskoppeln[gast].Length > 0)
                {
                    SpieleLeeren(D.AuswaertskoppelSpiele[(gast * D.N) + heim]);
                }
            }

            if (!Fest[s])
            {
                DatumLeeren(s);
            }

            anzahlOffen = Entfernen(offen, anzahlOffen, i);
            entfernt++;
            if (mitRueckspiel)
            {
                SpieleLeeren(D.Paarung[(heim * D.N) + gast]);
                SpieleLeeren(D.Paarung[(gast * D.N) + heim]);
            }
        }
    }

    /// <summary>Original <c>ClearGameList</c>.</summary>
    private void SpieleLeeren(ReadOnlySpan<int> spiele)
    {
        foreach (int s in spiele)
        {
            if (!Fest[s])
            {
                DatumLeeren(s);
            }
        }
    }

    /// <summary>Original <c>FillTermineIntern</c>.</summary>
    private void TermineFuellenIntern(bool alleZweitzeitenGueltig, Zufall zufall)
    {
        if (D.Rundenplanung != Rundenplanung.Beide)
        {
            NichtNotwendigeMarkieren();
        }

        int anzahlOffen = 0;
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if (Datum[s] == 0.0)
            {
                offen[anzahlOffen++] = s;
            }
        }

        while (anzahlOffen > 0)
        {
            int i = zufall.Zahl(anzahlOffen);
            int s = offen[i];
            if (!NichtNotwendig[s])
            {
                SpielFuellen(s, alleZweitzeitenGueltig, zufall);
            }

            anzahlOffen = Entfernen(offen, anzahlOffen, i);
        }

        Sortieren();
    }

    /// <summary>Ein Spiel aus <c>FillTermineIntern</c> terminieren, ggf. mit dem Auswärtskoppel-Partnerspiel.</summary>
    private void SpielFuellen(int s, bool alleZweitzeitenGueltig, Zufall zufall)
    {
        int heim = Heim[s];
        int anzahl = D.WunschStart[heim + 1] - D.WunschStart[heim];
        for (int k = 0; k < anzahl; k++)
        {
            indexe[k] = k;
        }

        TerminFuellen(s, anzahl, alleZweitzeitenGueltig, zufall);

        if (!D.HatAuswaertsKoppeltermine || !zufall.Prozent(70))
        {
            return;
        }

        int[] abhaengig = D.AuswaertskoppelSpiele[(Gast[s] * D.N) + heim];
        if (abhaengig.Length == 0)
        {
            return;
        }

        int zweites = abhaengig[zufall.Zahl(abhaengig.Length)];
        int zweitesHeim = Heim[zweites];
        int start = D.WunschStart[zweitesHeim];
        int passend = 0;
        for (int k = 0; k < D.WunschStart[zweitesHeim + 1] - start; k++)
        {
            if (Math.Abs(Tag[s] - D.WunschTag[start + k]) <= 1)
            {
                indexe[passend++] = k;
            }
        }

        if (passend > 0)
        {
            TerminFuellen(zweites, passend, alleZweitzeitenGueltig, zufall);
        }
    }

    /// <summary>Original <c>FillTerminIntern</c> mit den ersten <paramref name="anzahl"/> Wunschtermin-Indizes in <c>indexe</c>.</summary>
    private void TerminFuellen(int s, int anzahl, bool alleZweitzeitenGueltig, Zufall zufall)
    {
        int heim = Heim[s];
        int gast = Gast[s];
        int start = D.WunschStart[heim];
        while (anzahl > 0)
        {
            int i = zufall.Zahl(anzahl);
            int w = start + indexe[i];
            Wunschterminoptionen optionen = D.WunschOptionen[w];
            bool gueltig = WunschSpiel[w] < 0 && D.WunschInPlanung[w];
            if (gueltig
                && Terminoptionen.Hat(optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit)
                && !Terminoptionen.Hat(optionen, Wunschterminoptionen.KoppelZweitzeit)
                && !D.TeamsAmSelbenTag[(gast * D.N) + heim])
            {
                gueltig = false;
            }

            if (gueltig && TerminIstGueltig(w, s, -1, alleZweitzeitenGueltig))
            {
                SetzeDatum(s, D.WunschDatum[w], optionen, D.WunschParallele[w], false, false, w);
                if (D.Rundenplanung != Rundenplanung.Beide)
                {
                    RueckspielNichtNotwendig(s);
                }

                return;
            }

            anzahl = Entfernen(indexe, anzahl, i);
        }
    }

    /// <summary>Original <c>RemoveNotValidSecondTimeGames</c>.</summary>
    private void UngueltigeZweitterminSpieleEntfernen()
    {
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            if ((Terminoptionen.Hat(Opt[s], Wunschterminoptionen.KoppelZweitzeit) || Terminoptionen.Hat(Opt[s], Wunschterminoptionen.AuswaertsKoppelZweitzeit))
                && !SpielIstGueltig(s))
            {
                DatumLeeren(s);
            }
        }
    }

    /// <summary>Original <c>CreateRasterOneRound</c>.</summary>
    private void RasterEinerRunde(int runde, bool rueckrunde, Zufall zufall)
    {
        double[] alle = D.RundenMontage[runde];
        int anzahlMontage = alle.Length;
        Array.Copy(alle, montage, anzahlMontage);
        while (anzahlMontage > D.N - 1)
        {
            int index = zufall.Zahl(anzahlMontage);
            Array.Copy(montage, index + 1, montage, index, anzahlMontage - index - 1);
            anzahlMontage--;
        }

        int platzhalter = -1;
        int anzahl = D.N;
        if (D.N % 2 == 0)
        {
            platzhalter = reihenfolge[D.N - 1];
            anzahl--;
        }

        for (int m = 0; m < anzahl; m++)
        {
            for (int spieltag = 0; spieltag < anzahlMontage; spieltag++)
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

                int gegner = m2 == m ? platzhalter : reihenfolge[m2];
                if (gegner < 0)
                {
                    continue;
                }

                bool rueckspiel = rueckrunde;
                if (spieltag % 2 == 0)
                {
                    rueckspiel = !rueckspiel;
                }

                if (spieltag >= anzahlMontage / 2.0)
                {
                    rueckspiel = !rueckspiel;
                }

                if (rueckspiel)
                {
                    SpielFuerRaster(reihenfolge[m], gegner, montage[spieltag]);
                }
                else
                {
                    SpielFuerRaster(gegner, reihenfolge[m], montage[spieltag]);
                }
            }
        }
    }

    /// <summary>Original <c>ScheduleGameForRaster</c>.</summary>
    private void SpielFuerRaster(int heim, int gast, double montag)
    {
        int[] spiele = D.Paarung[(heim * D.N) + gast];
        for (int w = D.WunschStart[heim]; w < D.WunschStart[heim + 1]; w++)
        {
            if (D.WunschMontag[w] != montag)
            {
                continue;
            }

            foreach (int s in spiele)
            {
                if (!NichtNotwendig[s] && Datum[s] == 0.0 && D.WunschInPlanung[w] && TerminIstGueltig(w, s, -1, false))
                {
                    SetzeDatum(s, D.WunschDatum[w], D.WunschOptionen[w], D.WunschParallele[w], false, false, w);
                    if (D.Rundenplanung != Rundenplanung.Beide)
                    {
                        RueckspielNichtNotwendig(s);
                    }

                    break;
                }
            }
        }
    }
}
