// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Eine Suche des Kompaktplaners (je Thread eine): randomisierte Tiefensuche, die die Paarungen genau einmal überdeckt.
/// Gewählt wird jeweils die Paarung mit den wenigsten passenden Spieltagen; deren Spieltage werden nach Kilometern
/// (mit Zufallsstreuung) und Verstößen sortiert, die ersten wenigen werden versucht. Jede Mannschaft spielt höchstens
/// einmal am Tag, richtet mindestens einen Spieltag aus und hat höchstens eine Einzelbegegnung.
/// </summary>
internal sealed class Kompaktsuche
{
    /// <summary>So viele Spieltage werden je Paarung versucht.</summary>
    private const int Breite = 4;

    /// <summary>Gewicht der Kilometer gegenüber dem Zufall; je Versuch eines davon.</summary>
    private static readonly double[] Gewichte = [0.3, 0.7, 0.9, 1.0];

    private readonly Kompaktmodell modell;
    private readonly Random zufall;
    private readonly bool[] gedeckt;
    private readonly bool[] belegt;
    private readonly int[] ausgerichtet;
    private readonly int[] einzel;
    private readonly List<Kompaktkandidat> gewaehlt = [];
    private readonly int knotengrenze;
    private int offen;
    private int ohneAusrichtung;
    private int verstoesse;
    private int erlaubt;
    private int erlaubtOhne;
    private bool nurVierer;
    private int knoten;
    private double gewicht;

    /// <summary>Initialisiert eine Suche.</summary>
    public Kompaktsuche(Kompaktmodell modell, int startwert)
    {
        this.modell = modell;
        zufall = new Random(startwert);
        gedeckt = new bool[modell.Paare];
        belegt = new bool[modell.N * modell.T];
        ausgerichtet = new int[modell.N];
        einzel = new int[modell.N];

        // Gelingt ein Versuch, dann meist fast ohne Rückschritte; lange Suchen lohnen sich nicht (Versuch 12.10.2026).
        knotengrenze = modell.Paare + 10;
    }

    /// <summary>Ein Suchversuch.</summary>
    /// <param name="erlaubteVerstoesse">So viele Verstöße gegen die Meldungen sind (über die festgehaltenen hinaus) erlaubt.</param>
    /// <param name="erlaubtOhneAusrichtung">So viele Mannschaften dürfen ohne Heimspieltag bleiben.</param>
    /// <param name="nurViererSpieltage">Nur 4er-Spieltage (und Einzelbegegnungen) verwenden.</param>
    /// <returns>Alle Spieltage einschließlich der festgehaltenen oder <c>null</c>, wenn der Versuch keinen Plan fand.</returns>
    public IReadOnlyList<Kompaktkandidat>? Versuch(int erlaubteVerstoesse, int erlaubtOhneAusrichtung, bool nurViererSpieltage)
    {
        Zuruecksetzen();
        erlaubt = erlaubteVerstoesse;
        erlaubtOhne = erlaubtOhneAusrichtung;
        nurVierer = nurViererSpieltage;
        gewicht = Gewichte[zufall.Next(Gewichte.Length)];
        knoten = 0;
        return Suchen() ? gewaehlt.ToList() : null;
    }

    private void Zuruecksetzen()
    {
        Array.Clear(gedeckt);
        Array.Clear(belegt);
        Array.Clear(ausgerichtet);
        Array.Clear(einzel);
        gewaehlt.Clear();
        offen = modell.Paare;
        ohneAusrichtung = modell.N;
        foreach (Kompaktkandidat k in modell.Fest)
        {
            Setzen(k, 1);
        }

        verstoesse = 0;
    }

    private bool Suchen()
    {
        knoten++;
        if (knoten > knotengrenze)
        {
            return false;
        }

        if (offen == 0)
        {
            return ohneAusrichtung <= erlaubtOhne;
        }

        // Jeder weitere Dreier oder Vierer deckt mindestens drei (nur Vierer: vier) Paarungen und hat genau einen Ausrichter.
        if ((ohneAusrichtung - erlaubtOhne) * (nurVierer ? 4 : 3) > offen)
        {
            return false;
        }

        int paar = EngstesPaar();
        if (paar < 0)
        {
            return false;
        }

        foreach (Kompaktkandidat k in Reihenfolge(paar))
        {
            Setzen(k, 1);
            if (Suchen())
            {
                return true;
            }

            Setzen(k, -1);
        }

        return false;
    }

    /// <summary>Die offene Paarung mit den wenigsten passenden Spieltagen; -1, wenn eine keinen mehr hat.</summary>
    private int EngstesPaar()
    {
        int bestes = -1;
        int wenigste = int.MaxValue;
        for (int p = 0; p < modell.Paare; p++)
        {
            if (gedeckt[p])
            {
                continue;
            }

            int anzahl = Zaehlen(p, wenigste);
            if (anzahl < wenigste)
            {
                wenigste = anzahl;
                bestes = p;
                if (anzahl <= 1)
                {
                    break;
                }
            }
        }

        return wenigste == 0 ? -1 : bestes;
    }

    private int Zaehlen(int paar, int grenze)
    {
        int rest = erlaubt - verstoesse;
        int anzahl = 0;
        foreach (Kompaktkandidat k in Liste(paar))
        {
            if (k.Verstoesse > rest || anzahl >= grenze)
            {
                break;
            }

            anzahl += Passt(k) ? 1 : 0;
        }

        return anzahl;
    }

    private List<Kompaktkandidat> Reihenfolge(int paar)
    {
        int rest = erlaubt - verstoesse;
        var liste = new List<(double Wert, Kompaktkandidat Kandidat)>();
        foreach (Kompaktkandidat k in Liste(paar))
        {
            if (k.Verstoesse > rest)
            {
                break;
            }

            if (Passt(k))
            {
                liste.Add((Bewerten(k), k));
            }
        }

        liste.Sort((a, b) => a.Wert.CompareTo(b.Wert));
        return liste.Take(Breite).Select(x => x.Kandidat).ToList();
    }

    private Kompaktkandidat[] Liste(int paar) => nurVierer ? modell.JePaarOhneDreier[paar] : modell.JePaar[paar];

    /// <summary>Kleiner ist besser: Verstöße, Kilometer mit Zufallsstreuung, Bonus für Mannschaften ohne Heimspieltag und für 4er-Spieltage.</summary>
    private double Bewerten(Kompaktkandidat k)
    {
        double skala = modell.KmSkala;
        double wert = (k.Verstoesse * 10 * skala) + (k.Km * gewicht) + (zufall.NextDouble() * 2 * skala * (1 - gewicht));
        if (!k.IstEinzel && ausgerichtet[k.Ausrichter] == 0)
        {
            wert -= skala;
        }

        return k.Form == Turnierform.Vierer && modell.ViererBevorzugen ? wert - (skala / 2) : wert;
    }

    private bool Passt(Kompaktkandidat k)
    {
        int[] paare = k.Paare;
        for (int i = 0; i < paare.Length; i++)
        {
            if (gedeckt[paare[i]])
            {
                return false;
            }
        }

        int[] mannschaften = k.Mannschaften;
        for (int i = 0; i < mannschaften.Length; i++)
        {
            if (belegt[(mannschaften[i] * modell.T) + k.Tag])
            {
                return false;
            }
        }

        return !k.IstEinzel || (einzel[mannschaften[0]] == 0 && einzel[mannschaften[1]] == 0);
    }

    private void Setzen(Kompaktkandidat k, int schritt)
    {
        bool an = schritt > 0;
        foreach (int p in k.Paare)
        {
            gedeckt[p] = an;
        }

        foreach (int m in k.Mannschaften)
        {
            belegt[(m * modell.T) + k.Tag] = an;
        }

        offen -= schritt * k.Paare.Length;
        verstoesse += schritt * k.Verstoesse;
        if (k.IstEinzel)
        {
            einzel[k.Mannschaften[0]] += schritt;
            einzel[k.Mannschaften[1]] += schritt;
        }
        else
        {
            AusrichtungZaehlen(k.Ausrichter, schritt);
        }

        if (an)
        {
            gewaehlt.Add(k);
        }
        else
        {
            gewaehlt.RemoveAt(gewaehlt.Count - 1);
        }
    }

    private void AusrichtungZaehlen(int mannschaft, int schritt)
    {
        if (schritt > 0 && ausgerichtet[mannschaft] == 0)
        {
            ohneAusrichtung--;
        }

        ausgerichtet[mannschaft] += schritt;
        if (schritt < 0 && ausgerichtet[mannschaft] == 0)
        {
            ohneAusrichtung++;
        }
    }
}
