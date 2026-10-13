// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Vollständige Suche nach einem Plan ganz ohne Verstöße gegen die Meldungen (nicht im Original): aus 3er- und
/// 4er-Spieltagen, notfalls mit Einzelbegegnungen (Peter, 13.10.2026: „Notfalls plane lieber ein Einzelspiel ein.“). Sie
/// läuft, wenn die übrige Suche nur Pläne mit Verstößen fand. Verzweigt wird über die Paarung mit den wenigsten passenden
/// Spieltagen; Einzelbegegnungen nur, wenn nichts anderes passt. Abgeschnitten wird, sobald die noch offenen Paarungen
/// nicht mehr für einen Heimspieltag jeder Mannschaft ohne einen reichen. Zuerst die Hälfte der Zeit kurze Versuche in
/// zufälliger Reihenfolge (findet Pläne schnell, wo es viele gibt), dann die vollständige Suche, der kürzeste Spieltag
/// zuerst (beweist auch, dass es keinen gibt). Ohne Ergebnis innerhalb der Frist bleibt das offen (<see cref="Abgebrochen"/>).
/// </summary>
internal sealed class Meldungsbaum
{
    /// <summary>So viele Knoten hat ein kurzer Versuch.</summary>
    private const long VersuchsKnoten = 4000;

    /// <summary>Höchstens so lange kurze Versuche.</summary>
    private static readonly TimeSpan VersuchsZeit = TimeSpan.FromSeconds(10);

    private readonly Kompaktmodell modell;
    private readonly Kompaktkandidat[][] jePaar;
    private readonly bool[] gedeckt;
    private readonly bool[] belegt;
    private readonly int[] ausgerichtet;
    private readonly int[] heimspiele;
    private readonly List<Kompaktkandidat> gewaehlt = [];
    private Random? zufall;
    private long knoten;
    private long knotengrenze;
    private bool erschoepft;
    private int einzel;
    private int maxEinzel;
    private int maxOhne;
    private bool einzelZaehlt;
    private TimeSpan frist;
    private Kompaktsteuerung? lenkung;
    private TimeSpan beginn;

    /// <summary>Bereitet die Suche vor: je Paarung die Spieltage ohne Verstoß, dazu alle Einzelbegegnungen ohne Verstoß.</summary>
    /// <param name="modell">Das Modell der Aufgabe.</param>
    public Meldungsbaum(Kompaktmodell modell)
    {
        this.modell = modell;
        var je = new List<Kompaktkandidat>[modell.Paare];
        for (int p = 0; p < modell.Paare; p++)
        {
            je[p] = modell.JePaar[p].Where(k => k.Verstoesse == 0 && !k.IstEinzel).ToList();
        }

        for (int h = 0; h < modell.N; h++)
        {
            for (int g = 0; g < modell.N; g++)
            {
                for (int d = 0; h != g && d < modell.T; d++)
                {
                    Kompaktkandidat k = modell.Kandidat(Turnierform.Einzel, [h, g], d);
                    if (k.Verstoesse == 0)
                    {
                        je[k.Paare[0]].Add(k);
                    }
                }
            }
        }

        jePaar = je.Select(l => l.OrderBy(k => k.IstEinzel).ThenBy(k => k.Km).ToArray()).ToArray();
        gedeckt = new bool[modell.Paare];
        belegt = new bool[modell.N * modell.T];
        ausgerichtet = new int[modell.N];
        heimspiele = new int[modell.N];
    }

    /// <summary>Holt einen Wert, der angibt, ob die letzte Suche wegen der Frist oder eines Abbruchs unvollständig blieb.</summary>
    public bool Abgebrochen { get; private set; }

    /// <summary>Holt die Rechenzeit der laufenden Suche (ohne Pausen).</summary>
    private TimeSpan Vergangen => lenkung is null ? TimeSpan.Zero : lenkung.Laufzeit - beginn;

    /// <summary>Sucht einen Plan ohne Verstoß.</summary>
    /// <param name="hoechstensEinzel">So viele Einzelbegegnungen sind erlaubt.</param>
    /// <param name="hoechstensOhne">So viele Mannschaften dürfen ohne Heimspiel bleiben.</param>
    /// <param name="einzelZaehlt">Ein Heimspiel in einer Einzelbegegnung zählt als Heimspiel der Mannschaft.</param>
    /// <param name="dauer">Längstens so lange.</param>
    /// <param name="steuerung">Pause und Beenden; die Dauer zählt nur die Rechenzeit.</param>
    /// <returns>Die Spieltage oder <c>null</c>.</returns>
    public IReadOnlyList<Kompaktkandidat>? Suchen(int hoechstensEinzel, int hoechstensOhne, bool einzelZaehlt, TimeSpan dauer, Kompaktsteuerung steuerung)
    {
        maxEinzel = hoechstensEinzel;
        maxOhne = hoechstensOhne;
        this.einzelZaehlt = einzelZaehlt;
        frist = dauer;
        lenkung = steuerung;
        Abgebrochen = false;
        beginn = steuerung.Laufzeit;

        // Kurze Versuche in zufälliger Reihenfolge, höchstens die halbe Zeit und höchstens zehn Sekunden. Endet ein Versuch
        // ohne an die Knotengrenze zu stoßen, ist alles durchsucht: Es gibt keinen Plan.
        TimeSpan versuchszeit = TimeSpan.FromTicks(Math.Min(dauer.Ticks / 2, VersuchsZeit.Ticks));
        int versuch = 0;
        while (Vergangen < versuchszeit && !steuerung.Token.IsCancellationRequested)
        {
            zufall = new Random(++versuch);
            if (Lauf(VersuchsKnoten) is IReadOnlyList<Kompaktkandidat> gefunden)
            {
                return gefunden;
            }

            if (!erschoepft && !Abgebrochen)
            {
                return null;
            }
        }

        // Dann vollständig.
        zufall = null;
        return Lauf(long.MaxValue);
    }

    private IReadOnlyList<Kompaktkandidat>? Lauf(long grenze)
    {
        Array.Clear(gedeckt);
        Array.Clear(belegt);
        Array.Clear(ausgerichtet);
        Array.Clear(heimspiele);
        gewaehlt.Clear();
        einzel = 0;
        knoten = 0;
        knotengrenze = grenze;
        erschoepft = false;
        return Weiter() ? gewaehlt.ToList() : null;
    }

    private bool Weiter()
    {
        if ((knoten & 1023) == 0)
        {
            lenkung?.Warten();
        }

        if (lenkung?.Token.IsCancellationRequested == true || Vergangen > frist)
        {
            Abgebrochen = true;
            return false;
        }

        if (++knoten > knotengrenze)
        {
            erschoepft = true;
            return false;
        }

        if (OhneMoeglichkeit() > maxOhne || OhneZuViele())
        {
            return false;
        }

        int paar = -1;
        int wenigste = int.MaxValue;
        for (int p = 0; p < modell.Paare && wenigste > 1; p++)
        {
            if (gedeckt[p])
            {
                continue;
            }

            int zahl = jePaar[p].Count(Frei);
            if (zahl == 0)
            {
                return false;
            }

            if (zahl < wenigste)
            {
                (paar, wenigste) = (p, zahl);
            }
        }

        if (paar < 0)
        {
            return Ohne() <= maxOhne;
        }

        Kompaktkandidat[] wahl = jePaar[paar].Where(Frei).ToArray();
        if (zufall is not null)
        {
            zufall.Shuffle(wahl);
            Array.Sort(wahl, (a, b) => a.IstEinzel.CompareTo(b.IstEinzel));
        }

        foreach (Kompaktkandidat k in wahl)
        {
            Setzen(k, true);
            if (Weiter())
            {
                return true;
            }

            Setzen(k, false);
            if (Abgebrochen || erschoepft)
            {
                return false;
            }
        }

        return false;
    }

    private bool Frei(Kompaktkandidat k) =>
        (!k.IstEinzel || einzel < maxEinzel)
        && !Array.Exists(k.Paare, p => gedeckt[p])
        && !Array.Exists(k.Mannschaften, m => belegt[(m * modell.T) + k.Tag]);

    private void Setzen(Kompaktkandidat k, bool an)
    {
        int schritt = an ? 1 : -1;
        foreach (int p in k.Paare)
        {
            gedeckt[p] = an;
        }

        foreach (int m in k.Mannschaften)
        {
            belegt[(m * modell.T) + k.Tag] = an;
        }

        heimspiele[k.Ausrichter] += schritt;
        if (k.IstEinzel)
        {
            einzel += schritt;
        }
        else
        {
            ausgerichtet[k.Ausrichter] += schritt;
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

    /// <summary>
    /// Ob die offenen Paarungen nicht mehr für einen eigenen Heimspiel-Spieltag jeder Mannschaft ohne Heimspiel reichen
    /// (über die erlaubte Zahl hinaus): ein 3er- oder 4er-Spieltag braucht mindestens drei Paarungen, ein Einzelspiel eine.
    /// </summary>
    private bool OhneZuViele()
    {
        int offen = gedeckt.Count(g => !g);
        int restEinzel = einzelZaehlt ? Math.Min(offen, maxEinzel - einzel) : 0;
        int moeglich = restEinzel + ((offen - restEinzel) / 3);
        return Ohne() - maxOhne > moeglich;
    }

    private bool Hat(int t) => (einzelZaehlt ? heimspiele[t] : ausgerichtet[t]) > 0;

    private int Ohne() => Enumerable.Range(0, modell.N).Count(t => !Hat(t));

    /// <summary>Mannschaften ohne Heimspiel, die auch an keinem freien Sonntag mehr zuhause spielen können.</summary>
    private int OhneMoeglichkeit() =>
        Enumerable.Range(0, modell.N).Count(t => !Hat(t) && !Enumerable.Range(0, modell.T).Any(d => modell.KannHeim(t, d) && !belegt[(t * modell.T) + d]));
}
