// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Veränderlicher Zustand der schnellen Engine (eine Lösung) mit Gültigkeitsprüfung und Spiellisten.
/// Arbeitet ausschließlich auf Arrays; Semantik, Listenreihenfolgen und Reihenfolge der Zufallszahlen entsprechen dem
/// Referenzmodell, damit gleiche Startwerte zu gleichen Ergebnissen führen. Im inneren Durchlauf wird kein Speicher angefordert.
/// </summary>
internal sealed partial class KernPlan
{
    private const int MaxMannschaft = 30;
    private const int MaxRunde = 10;

    private readonly int anzahlErlaubt;
    private readonly int[] gemerktWunsch;
    private readonly double[] gemerktDatum;
    private readonly Wunschterminoptionen[] gemerktOptionen;
    private readonly int[] gemerktMaxHeim;
    private readonly bool[] gemerktNichtNotwendig;

    // Arbeitsspeicher der Kostenrechnung und der Optimierungsschritte (einmal angelegt, danach wiederverwendet).
    private readonly int[] positionen;
    private readonly int[] positionenAnzahl;
    private readonly int[] werteGanz;
    private readonly double[] werteKomma;
    private readonly bool[] koppelDanach;
    private readonly int[] jeRunde;
    private readonly int[] zaehler;
    private readonly double[] tage;
    private readonly double[] letzteMaxima;
    private readonly int[] auswahl;
    private readonly int[] offen;
    private readonly int[] indexe;
    private readonly double[] montage;
    private readonly int[] reihenfolge;
    private readonly int[] spieltagIndexe;

    // Kosten-Cache je Mannschaft und Kostenart: Stand der Spiele und Spiellisten bei der letzten Kostenrechnung.
    private readonly double[] cacheDatum;
    private readonly Wunschterminoptionen[] cacheOptionen;
    private readonly int[] cacheMaxHeim;
    private readonly bool[] cacheNichtNotwendig;
    private readonly int[] cacheLokal;
    private readonly int[][] cacheSpiele;
    private readonly int[] cacheSpieleAnzahl;
    private readonly bool[] geaendert;
    private readonly bool[] vereinGeaendert;
    private readonly bool[] artBerechnet;

    // Anzahl der Spiele je Mannschaft und Tag (nur Tage im Bereich der Wunschtermine, nur Spiele in den Spiellisten).
    private readonly int[] tagBelegung;

    private int anzahlAktiv;
    private int cacheAnzahlAktiv;

    /// <summary>Übernimmt Stammdaten und aktuellen Stand eines geladenen Referenzplans.</summary>
    public KernPlan(RefPlan plan)
        : this(plan, new KernDefinition(plan))
    {
    }

    /// <summary>
    /// Übernimmt den aktuellen Stand eines geladenen Referenzplans und nutzt gemeinsame Stammdaten
    /// (mehrere Pläne desselben Referenzplans teilen sich eine <see cref="KernDefinition"/>; nacheinander anlegen).
    /// </summary>
    public KernPlan(RefPlan plan, KernDefinition definition)
    {
        D = definition;
        anzahlErlaubt = D.SpielHeim.Length;
        int gesamt = anzahlErlaubt + plan.SpieleNichtErlaubt.Count;
        anzahlAktiv = gesamt;
        Heim = new int[gesamt];
        Gast = new int[gesamt];
        Datum = new double[gesamt];
        Tag = new int[gesamt];
        Runde = new int[gesamt];
        InPlanung = new bool[gesamt];
        Opt = new Wunschterminoptionen[gesamt];
        MaxHeim = new int[gesamt];
        Wunsch = new int[gesamt];
        Fest = new bool[gesamt];
        NichtNotwendig = new bool[gesamt];
        UeberLokal = new int[gesamt];
        gemerktWunsch = new int[gesamt];
        gemerktDatum = new double[gesamt];
        gemerktOptionen = new Wunschterminoptionen[gesamt];
        gemerktMaxHeim = new int[gesamt];
        gemerktNichtNotwendig = new bool[gesamt];
        WunschSpiel = new int[D.WunschDatum.Length];

        var alle = plan.SpieleErlaubt.Concat(plan.SpieleNichtErlaubt).ToList();
        var nummer = new Dictionary<RefSpiel, int>(ReferenceEqualityComparer.Instance);
        for (int s = 0; s < gesamt; s++)
        {
            nummer.Add(alle[s], s);
        }

        var wunschNummer = new Dictionary<RefWunschtermin, int>(ReferenceEqualityComparer.Instance);
        for (int w = 0; w < D.WunschRef.Length; w++)
        {
            wunschNummer.Add(D.WunschRef[w], w);
        }

        for (int s = 0; s < gesamt; s++)
        {
            RefSpiel spiel = alle[s];
            Heim[s] = spiel.Heim.Index;
            Gast[s] = spiel.Gast.Index;
            Datum[s] = spiel.Datum;
            Tag[s] = DelphiDatum.Trunc(spiel.Datum);
            Runde[s] = D.RundeZuDatum(spiel.Datum);
            InPlanung[s] = D.IstRundeZuPlanen(Runde[s]);
            Opt[s] = spiel.DatumOptionen;
            MaxHeim[s] = spiel.DatumMaxHeim;
            Wunsch[s] = spiel.Wunschtermin is null ? -1 : wunschNummer[spiel.Wunschtermin];
            Fest[s] = spiel.FestesDatum;
            NichtNotwendig[s] = spiel.NichtNotwendig;
            UeberLokal[s] = D.LokalId(spiel.UeberschriebenesLokal);
            gemerktWunsch[s] = -1;
        }

        for (int w = 0; w < D.WunschRef.Length; w++)
        {
            RefSpiel? zugeordnet = D.WunschRef[w].ZugeordnetesSpiel;
            WunschSpiel[w] = zugeordnet is not null && nummer.TryGetValue(zugeordnet, out int s) ? s : -1;
        }

        // Spiellisten: Kapazität = alle Spiele, an denen die Mannschaft überhaupt beteiligt sein kann.
        int[] kapazitaet = new int[D.N];
        for (int s = 0; s < gesamt; s++)
        {
            kapazitaet[Heim[s]]++;
            kapazitaet[Gast[s]]++;
        }

        Spiele = new int[D.N][];
        SpieleAnzahl = new int[D.N];
        for (int t = 0; t < D.N; t++)
        {
            Spiele[t] = new int[kapazitaet[t]];
            List<RefSpiel> liste = plan.Mannschaften[t].Spiele;
            for (int i = 0; i < liste.Count; i++)
            {
                Spiele[t][i] = nummer[liste[i]];
            }

            SpieleAnzahl[t] = liste.Count;
        }

        AlleTermineGueltig = plan.AlleTermineGueltig;
        tagBelegung = new int[D.N * D.AnzahlTage];
        BelegungNeuAufbauen();

        int maxSpiele = kapazitaet.Length == 0 ? 0 : kapazitaet.Max();
        int runden = Math.Max(1, D.Runden.Length);
        int maxWunsch = 0;
        for (int t = 0; t < D.N; t++)
        {
            maxWunsch = Math.Max(maxWunsch, D.WunschStart[t + 1] - D.WunschStart[t]);
        }

        positionen = new int[D.N * MaxRunde];
        positionenAnzahl = new int[D.N];
        werteGanz = new int[D.N * MaxRunde];
        werteKomma = new double[runden * (maxSpiele + 2)];
        koppelDanach = new bool[maxSpiele + 1];
        jeRunde = new int[runden];
        zaehler = new int[Math.Max(MaxMannschaft + 1, D.N)];
        tage = new double[D.N];
        letzteMaxima = new double[maxSpiele + 1];
        int listen = Math.Max(gesamt, 9 * Math.Max(maxSpiele, D.N));
        auswahl = new int[listen];
        offen = new int[listen];
        indexe = new int[maxWunsch];
        montage = new double[D.RundenMontage.Length == 0 ? 0 : D.RundenMontage.Max(m => m.Length)];
        reihenfolge = new int[D.N];
        spieltagIndexe = new int[D.N];

        cacheDatum = new double[gesamt];
        cacheOptionen = new Wunschterminoptionen[gesamt];
        cacheMaxHeim = new int[gesamt];
        cacheNichtNotwendig = new bool[gesamt];
        cacheLokal = new int[gesamt];
        cacheSpiele = kapazitaet.Select(k => new int[k]).ToArray();
        cacheSpieleAnzahl = new int[D.N];
        geaendert = new bool[D.N];
        vereinGeaendert = new bool[D.N];
        artBerechnet = new bool[KernDefinition.AnzahlArten];

        KostenAnzahl = new int[D.N * KernDefinition.AnzahlArten];
        KostenAlle = new int[D.N * KernDefinition.AnzahlArten];
        KostenWert = new double[D.N * KernDefinition.AnzahlArten];
    }

    public KernDefinition D { get; }

    public int AnzahlErlaubt => anzahlErlaubt;

    /// <summary>Erlaubte und (noch vorhandene) nicht erlaubte Spiele; Letztere liegen hinter den erlaubten.</summary>
    public int AnzahlAktiv => anzahlAktiv;

    public int[] Heim { get; }

    public int[] Gast { get; }

    public double[] Datum { get; }

    public int[] Tag { get; }

    /// <summary>Runde des Termins je Spiel (<see cref="KernDefinition.RundeZuDatum"/>), mit dem Datum gepflegt.</summary>
    public int[] Runde { get; }

    /// <summary>Termin liegt in einer zu planenden Runde (<see cref="KernDefinition.IstInZuPlanenderRunde"/>).</summary>
    public bool[] InPlanung { get; }

    public Wunschterminoptionen[] Opt { get; }

    public int[] MaxHeim { get; }

    public int[] Wunsch { get; }

    public bool[] Fest { get; }

    public bool[] NichtNotwendig { get; }

    public int[] UeberLokal { get; }

    /// <summary>Spiel, dem ein Wunschtermin zugeordnet ist (Original <c>assignedGame</c>), −1 = frei.</summary>
    public int[] WunschSpiel { get; }

    /// <summary>Spiele je Mannschaft, nach Datum sortiert (Original <c>gamesRef</c>).</summary>
    public int[][] Spiele { get; }

    public int[] SpieleAnzahl { get; }

    public bool AlleTermineGueltig { get; set; }

    /// <summary>Anzahl je Mannschaft und Kostenart (Index t·16 + Art).</summary>
    public int[] KostenAnzahl { get; }

    /// <summary>Bezugsgröße je Mannschaft und Kostenart (Original <c>AllCount</c>).</summary>
    public int[] KostenAlle { get; }

    /// <summary>Kosten je Mannschaft und Kostenart.</summary>
    public double[] KostenWert { get; }

    /// <summary>Original <c>setDate</c>.</summary>
    public void SetzeDatum(int s, double datum, Wunschterminoptionen optionen, int maxHeim, bool fest, bool nichtNotwendig, int wunsch)
    {
        DatumSetzen(s, datum);
        Opt[s] = optionen;
        MaxHeim[s] = maxHeim;
        Fest[s] = fest;
        NichtNotwendig[s] = nichtNotwendig;
        if (Wunsch[s] >= 0)
        {
            WunschSpiel[Wunsch[s]] = -1;
        }

        Wunsch[s] = wunsch;
        if (wunsch >= 0)
        {
            WunschSpiel[wunsch] = s;
        }
    }

    /// <summary>Original <c>setDate(0.0, [], 0, false, false, nil)</c>.</summary>
    public void DatumLeeren(int s) => SetzeDatum(s, 0.0, default, 0, false, false, -1);

    /// <summary>Original <c>ClearAllValues</c> (ohne Rückwirkung auf den Wunschtermin).</summary>
    public void AlleWerteLoeschen(int s)
    {
        DatumSetzen(s, 0.0);
        Opt[s] = default;
        MaxHeim[s] = 0;
        Fest[s] = false;
        Wunsch[s] = -1;
        NichtNotwendig[s] = false;
        gemerktDatum[s] = 0.0;
        gemerktOptionen[s] = default;
        gemerktMaxHeim[s] = 0;
        gemerktWunsch[s] = -1;
        gemerktNichtNotwendig[s] = false;
        UeberLokal[s] = 0;
    }

    /// <summary>Original <c>getLocation</c>: Nummer des Spiellokals.</summary>
    public int Lokal(int s)
    {
        if (UeberLokal[s] != 0)
        {
            return UeberLokal[s];
        }

        return Wunsch[s] >= 0 ? D.WunschLokal[Wunsch[s]] : 0;
    }

    /// <summary>Original <c>GenerateMannschaftsRefGames</c> mit <c>SortMannschaftsDates</c>.</summary>
    public void SpiellistenAufbauen()
    {
        Array.Clear(SpieleAnzahl);
        for (int s = 0; s < anzahlAktiv; s++)
        {
            Spiele[Heim[s]][SpieleAnzahl[Heim[s]]++] = s;
            Spiele[Gast[s]][SpieleAnzahl[Gast[s]]++] = s;
        }

        BelegungNeuAufbauen();
        Sortieren();
    }

    /// <summary>Original <c>SortMannschaftsDates</c> (Austauschverfahren wie <c>TGameList.SortByDate</c>).</summary>
    public void Sortieren()
    {
        for (int t = 0; t < D.N; t++)
        {
            int[] liste = Spiele[t];
            int anzahl = SpieleAnzahl[t];
            int i = 0;
            while (i < anzahl - 1)
            {
                if (Vergleichen(liste[i], liste[i + 1]) > 0)
                {
                    (liste[i], liste[i + 1]) = (liste[i + 1], liste[i]);
                    i = Math.Max(0, i - 1);
                }
                else
                {
                    i++;
                }
            }
        }
    }

    /// <summary>Original <c>getKoppeledGameAfter</c>; −1 = keins.</summary>
    public int GekoppeltDanach(int t, int index)
    {
        int[] liste = Spiele[t];
        if (index >= 0 && index < SpieleAnzahl[t] - 1 && IstGekoppelt(t, liste[index], liste[index + 1]))
        {
            return GekoppeltDanach(t, index + 1) >= 0 ? -1 : liste[index + 1];
        }

        return -1;
    }

    /// <summary>Original <c>getKoppeledGameBefore</c>.</summary>
    public int GekoppeltDavor(int t, int index) => index > 0 && GekoppeltDanach(t, index - 1) >= 0 ? Spiele[t][index - 1] : -1;

    /// <summary>Original <c>getKoppeledGame(Index)</c>.</summary>
    public int Gekoppelt(int t, int index)
    {
        int ergebnis = GekoppeltDanach(t, index);
        if (ergebnis < 0 && index > 0 && Spiele[t][index] == GekoppeltDanach(t, index - 1))
        {
            ergebnis = Spiele[t][index - 1];
        }

        return ergebnis;
    }

    /// <summary>Original <c>IsKoppelTermin</c>.</summary>
    public Wunschterminoptionen KoppelOptionen(int t, int s) => Heim[s] == t ? Opt[s] : default;

    /// <summary>Original <c>InternIsKoppeledGames</c>.</summary>
    public bool IstGekoppelt(int t, int s1, int s2)
    {
        if (Math.Abs(Tag[s1] - Tag[s2]) > 1)
        {
            return false;
        }

        bool ergebnis = Heim[s1] == t && Heim[s2] == t
            && Terminoptionen.IstKoppel(KoppelOptionen(t, s1)) && Terminoptionen.IstKoppel(KoppelOptionen(t, s2));
        if (Gast[s1] == t && Gast[s2] == t && GueltigesAuswaertsKoppelDatum(t, Datum[s1], Datum[s2], Heim[s1], Heim[s2]))
        {
            ergebnis = true;
        }

        return ergebnis;
    }

    /// <summary>Original <c>IsValidAuswaertsKoppelDate</c>.</summary>
    public bool GueltigesAuswaertsKoppelDatum(int t, double datum1, double datum2, int teamA, int teamB)
    {
        int abstand = Math.Abs(DelphiDatum.Trunc(datum1) - DelphiDatum.Trunc(datum2));
        if (abstand > 1)
        {
            return false;
        }

        bool ergebnis = false;
        foreach ((int a, int b, AuswaertsKoppelTyp art) in D.Auswaertskoppeln[t])
        {
            bool gueltig = art switch
            {
                AuswaertsKoppelTyp.Keiner => false,
                AuswaertsKoppelTyp.GleicherTag => abstand == 0 && Math.Abs(datum1 - datum2) >= DelphiDatum.MinGameDistance,
                AuswaertsKoppelTyp.AndererTag => abstand == 1,
                _ => abstand <= 1 && Math.Abs(datum1 - datum2) >= DelphiDatum.MinGameDistance,
            };
            if (gueltig && a >= 0 && b >= 0 && ((teamA == a && teamB == b) || (teamA == b && teamB == a)))
            {
                ergebnis = true;
            }
        }

        return ergebnis;
    }

    /// <summary>Original <c>IsTerminFree</c>.</summary>
    public bool IstTerminFrei(int t, int wunsch, int heim, int gast, int ignorieren, bool alleZweitzeitenGueltig)
    {
        bool ergebnis = true;
        double datum = D.WunschDatum[wunsch];
        int tag = D.WunschTag[wunsch];
        Wunschterminoptionen optionen = D.WunschOptionen[wunsch];
        if (!alleZweitzeitenGueltig)
        {
            if (Terminoptionen.Hat(optionen, Wunschterminoptionen.KoppelZweitzeit) && heim == t)
            {
                ergebnis = false;
            }

            if (Terminoptionen.Hat(optionen, Wunschterminoptionen.AuswaertsKoppelZweitzeit) && gast == t)
            {
                ergebnis = false;
            }
        }

        // Schnellweg über den Belegungszähler: kein anderes Spiel der Mannschaft an diesem Tag → die Schleife fände nichts.
        int andere = tagBelegung[(t * D.AnzahlTage) + tag - D.ErsterTag];
        if (ignorieren >= 0 && ignorieren < anzahlAktiv && Tag[ignorieren] == tag && (Heim[ignorieren] == t || Gast[ignorieren] == t))
        {
            andere--;
        }

        if (andere == 0)
        {
            return ergebnis;
        }

        if (!D.HatAuswaertsKoppeltermine && !D.HatKoppeltermine)
        {
            return false;
        }

        int[] liste = Spiele[t];
        for (int i = 0; i < SpieleAnzahl[t]; i++)
        {
            int s = liste[i];
            if (s == ignorieren || Tag[s] != tag)
            {
                continue;
            }

            if (!D.HatAuswaertsKoppeltermine && !D.HatKoppeltermine)
            {
                return false;
            }

            if (Datum[s] <= 0)
            {
                continue;
            }

            if (Math.Abs(Datum[s] - datum) < DelphiDatum.MinGameDistance)
            {
                return false;
            }

            if (D.HatAuswaertsKoppeltermine && gast == t && Gast[s] == t && GueltigesAuswaertsKoppelDatum(t, Datum[s], datum, heim, Heim[s]))
            {
                ergebnis = true;
                continue;
            }

            if (t != heim || !Terminoptionen.IstKoppel(optionen) || !Terminoptionen.IstKoppel(KoppelOptionen(t, s)))
            {
                return false;
            }

            ergebnis = true;
        }

        return ergebnis;
    }

    /// <summary>Original <c>TerminIsValid</c>; <paramref name="ignorieren"/> = −1 für keins.</summary>
    public bool TerminIstGueltig(int wunsch, int s, int ignorieren, bool alleZweitzeitenGueltig)
    {
        if (D.WunschSpielfrei[wunsch])
        {
            return false;
        }

        int heim = Heim[s];
        int gast = Gast[s];
        if (!IstTerminFrei(heim, wunsch, heim, gast, ignorieren, alleZweitzeitenGueltig)
            || !IstTerminFrei(gast, wunsch, heim, gast, ignorieren, alleZweitzeitenGueltig))
        {
            return false;
        }

        int runde = D.WunschRunde[wunsch];
        foreach (int t in D.Paarung[(heim * D.N) + gast])
        {
            if (t != ignorieren && Datum[t] != 0.0 && runde / 2 == Runde[t] / 2)
            {
                return false;
            }
        }

        foreach (int t in D.Paarung[(gast * D.N) + heim])
        {
            if (t != ignorieren && Datum[t] != 0.0 && runde == Runde[t])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Original <c>GameIsValid</c>.</summary>
    public bool SpielIstGueltig(int s)
    {
        if (!InPlanung[s])
        {
            return D.Rundenplanung is Rundenplanung.NurRueckrunde or Rundenplanung.Corona
                && (IstFesterTermin(D.Bestehend, s) || IstFesterTermin(D.Vorgegeben, s));
        }

        int zugeordnet = Wunsch[s];
        if (zugeordnet >= 0 && D.WunschDatum[zugeordnet] == Datum[s])
        {
            return TerminIstGueltig(D.WunschErster[zugeordnet], s, s, false);
        }

        int heim = Heim[s];
        for (int w = D.WunschStart[heim]; w < D.WunschStart[heim + 1]; w++)
        {
            if (D.WunschDatum[w] == Datum[s])
            {
                return TerminIstGueltig(w, s, s, false);
            }
        }

        return IstFesterTermin(D.Vorgegeben, s);
    }

    /// <summary>Original <c>MarkSecondGameAsUnNecessary</c>.</summary>
    public void RueckspielNichtNotwendig(int s)
    {
        foreach (int r in D.Paarung[(Gast[s] * D.N) + Heim[s]])
        {
            if (!NichtNotwendig[r] && Datum[r] == 0.0)
            {
                SetzeDatum(r, Datum[r], default, 0, false, true, -1);
                return;
            }
        }
    }

    /// <summary>Original <c>MarkNotNecessaryGames</c>.</summary>
    public void NichtNotwendigeMarkieren()
    {
        for (int s = 0; s < anzahlErlaubt; s++)
        {
            SetzeDatum(s, Datum[s], Opt[s], MaxHeim[s], Fest[s], false, Wunsch[s]);
        }

        if (D.Rundenplanung == Rundenplanung.Beide)
        {
            return;
        }

        for (int s = 0; s < anzahlErlaubt; s++)
        {
            bool markieren = D.Rundenplanung == Rundenplanung.Corona ? Datum[s] != 0.0 : InPlanung[s];
            if (markieren)
            {
                RueckspielNichtNotwendig(s);
            }
        }
    }

    /// <summary>Setzt das Datum eines Spiels samt Tag, Runde und Belegungszählern.</summary>
    private void DatumSetzen(int s, double datum)
    {
        Datum[s] = datum;
        Runde[s] = D.RundeZuDatum(datum);
        InPlanung[s] = D.IstRundeZuPlanen(Runde[s]);
        TagSetzen(s, DelphiDatum.Trunc(datum));
    }

    /// <summary>Setzt den Tag eines Spiels und hält die Belegungszähler aktuell.</summary>
    private void TagSetzen(int s, int tag)
    {
        if (s < anzahlAktiv)
        {
            BelegungAendern(s, Tag[s], -1);
            BelegungAendern(s, tag, 1);
        }

        Tag[s] = tag;
    }

    private void BelegungAendern(int s, int tag, int delta)
    {
        int index = tag - D.ErsterTag;
        if (index >= 0 && index < D.AnzahlTage)
        {
            tagBelegung[(Heim[s] * D.AnzahlTage) + index] += delta;
            tagBelegung[(Gast[s] * D.AnzahlTage) + index] += delta;
        }
    }

    private void BelegungNeuAufbauen()
    {
        Array.Clear(tagBelegung);
        for (int s = 0; s < anzahlAktiv; s++)
        {
            BelegungAendern(s, Tag[s], 1);
        }
    }

    private bool IstFesterTermin(FesterTermin[] termine, int s)
    {
        foreach (FesterTermin f in termine)
        {
            if (f.Datum == Datum[s] && D.IdKlasse[f.Heim] == D.IdKlasse[Heim[s]] && D.IdKlasse[f.Gast] == D.IdKlasse[Gast[s]])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Original <c>compGameListByDate</c>.</summary>
    private int Vergleichen(int a, int b)
    {
        if (Datum[a] > Datum[b])
        {
            return 1;
        }

        if (Datum[a] < Datum[b])
        {
            return -1;
        }

        return D.PaarungsRang[(Heim[a] * D.N) + Gast[a]].CompareTo(D.PaarungsRang[(Heim[b] * D.N) + Gast[b]]);
    }
}
