// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Vollständige Suche nach dem kürzesten Plan nur aus 4er-Spieltagen (Branch and Bound, nicht im Original), wenn die
/// Paarungen genau aufgehen und jede Mannschaft genau einen 4er-Spieltag ausrichtet: 9 Mannschaften, oder 10 mit je einer
/// Einzelbegegnung. Nur Spieltage ohne Verstoß gegen die Meldungen mit mindestens einem möglichen Sonntag. Verzweigt wird
/// über die Paarung mit den wenigsten passenden Spieltagen, der kürzeste zuerst; abgeschnitten wird mit einer unteren
/// Schranke der noch fehlenden Fahrten (je Gast die nächsten freien Gastgeber, je Gastgeber die nächsten Gäste). Die
/// Sonntage werden für jeden kürzeren vollständigen Plan verteilt; geht das nicht, zählt er nicht. Die ersten
/// Verzweigungen laufen parallel.
/// </summary>
internal sealed class Viererbaum
{
    private const int Meldetakt = 1 << 14;
    private const int GaesteJeSpieltag = 3;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly Kompaktmodell modell;
    private readonly List<Viererblock> bloecke = [];
    private readonly List<Viererblock> feste = [];
    private readonly int[][] jePaar;
    private readonly int[][] gastgeberNachNaehe;
    private readonly int[][] gaesteNachNaehe;
    private readonly int gastSoll;
    private readonly ulong alle;
    private readonly Lock sperre = new();
    private readonly Stopwatch uhr = new();
    private double beste = double.MaxValue;
    private long knoten;
    private long naechsteMeldung;

    /// <summary>Bereitet die Suche vor.</summary>
    public Viererbaum(Kompaktmodell modell)
    {
        this.modell = modell;
        int n = modell.N;
        alle = modell.Paare >= 64 ? ulong.MaxValue : (1UL << modell.Paare) - 1;

        // Je Mannschaft (n - 1) bzw. (n - 2) Gegner in 4er-Spieltagen, zwei je Spieltag, einer davon als Ausrichter.
        gastSoll = ((n % 2 == 0 ? n - 2 : n - 1) / 2) - 1;
        BloeckeErzeugen();
        feste.AddRange(modell.Fest.Select(k => Block(k.Form, k.Mannschaften, 1UL << k.Tag)));
        jePaar = Enumerable.Range(0, modell.Paare)
            .Select(p => Enumerable.Range(0, bloecke.Count).Where(b => ((bloecke[b].Paare >> p) & 1) != 0).OrderBy(b => bloecke[b].Km).ToArray())
            .ToArray();
        gastgeberNachNaehe = Enumerable.Range(0, n).Select(t => Enumerable.Range(0, n).Where(h => h != t).OrderBy(h => modell.Entfernung(t, h)).ToArray()).ToArray();
        gaesteNachNaehe = Enumerable.Range(0, n).Select(h => Enumerable.Range(0, n).Where(t => t != h).OrderBy(t => modell.Entfernung(t, h)).ToArray()).ToArray();
    }

    /// <summary>Holt den kürzesten gefundenen Plan mit Sonntagen oder <c>null</c>.</summary>
    public IReadOnlyList<Kompaktkandidat>? Beste { get; private set; }

    /// <summary>Holt einen Wert, der angibt, ob alle Pläne durchsucht wurden (der gefundene ist dann der kürzeste).</summary>
    public bool Vollstaendig { get; private set; }

    /// <summary>Holt die Zahl der geprüften Teilpläne.</summary>
    public long Knoten => Interlocked.Read(ref knoten);

    /// <summary>Ob die vollständige Suche für diese Aufgabe geht: 9 oder 10 Mannschaften, keine festgehaltenen 3er-Spieltage.</summary>
    public static bool Moeglich(Kompaktmodell modell)
    {
        int turnierpaare = modell.Paare - (modell.N % 2 == 0 ? modell.N / 2 : 0);
        return modell.N is >= 5 and <= 11
            && modell.T is > 0 and <= 64
            && turnierpaare == 4 * modell.N
            && modell.Fest.All(k => k.Form != Turnierform.Dreier);
    }

    /// <summary>Sucht, bis alles durchsucht, die Zeit abgelaufen oder abgebrochen ist.</summary>
    public void Suchen(int threads, TimeSpan dauer, CancellationToken abbruch, Action<string>? fortschritt)
    {
        using var zeit = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        zeit.CancelAfter(dauer);
        CancellationToken token = zeit.Token;
        uhr.Start();
        var wurzel = new Viererzweig(modell.N);
        foreach (Viererblock b in feste)
        {
            wurzel.Setzen(b, 1);
        }

        List<int>? anfang = wurzel.Gedeckt == alle ? null : Kandidaten(wurzel);
        if (wurzel.Gedeckt == alle)
        {
            Blatt(wurzel);
        }
        else if (anfang is not null)
        {
            try
            {
                var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = token };
                Parallel.ForEach(anfang, parallel, b => Teilbaum(wurzel, bloecke[b], token, fortschritt));
            }
            catch (OperationCanceledException)
            {
                // Zeit abgelaufen oder abgebrochen: der bis dahin kürzeste Plan gilt.
                Vollstaendig = false;
                return;
            }
        }

        Vollstaendig = !token.IsCancellationRequested;
    }

    private static bool Passt(Viererzweig z, Viererblock b) =>
        (b.Paare & z.Gedeckt) == 0
        && (b.IstEinzel ? (z.Einzel & b.TeamMaske) == 0 : ((z.Ausrichter >> b.Ausrichter) & 1) == 0);

    private Viererblock Block(Turnierform form, int[] mannschaften, ulong tage)
    {
        ulong paare = 0;
        foreach (int p in modell.PaareFuer(form, mannschaften))
        {
            paare |= 1UL << p;
        }

        int maske = mannschaften.Aggregate(0, (m, t) => m | (1 << t));
        return new Viererblock(form, mannschaften, paare, maske, modell.StreckeFuer(mannschaften), tage);
    }

    /// <summary>Alle 4er-Spieltage (bei gerader Zahl auch Einzelbegegnungen) ohne Verstoß an mindestens einem Sonntag.</summary>
    private void BloeckeErzeugen()
    {
        int n = modell.N;
        for (int h = 0; h < n; h++)
        {
            for (int b = 0; b < n; b++)
            {
                ViererFuer(h, b);
                if (n % 2 == 0 && b != h)
                {
                    Hinzufuegen(Turnierform.Einzel, [h, b]);
                }
            }
        }
    }

    private void ViererFuer(int h, int b)
    {
        int n = modell.N;
        for (int c = 0; c < n; c++)
        {
            for (int e = c + 1; e < n; e++)
            {
                if (b != h && c != h && c != b && e != h && e != b)
                {
                    Hinzufuegen(Turnierform.Vierer, [h, b, c, e]);
                }
            }
        }
    }

    private void Hinzufuegen(Turnierform form, int[] mannschaften)
    {
        if (!modell.Wunschgerecht(mannschaften, modell.PaareFuer(form, mannschaften)))
        {
            return;
        }

        ulong tage = 0;
        for (int d = 0; d < modell.T; d++)
        {
            if (modell.Verstoesse(form, mannschaften, d) == 0)
            {
                tage |= 1UL << d;
            }
        }

        if (tage != 0)
        {
            bloecke.Add(Block(form, mannschaften, tage));
        }
    }

    private void Teilbaum(Viererzweig wurzel, Viererblock erster, CancellationToken token, Action<string>? fortschritt)
    {
        if (wurzel.Km + erster.Km >= Volatile.Read(ref beste))
        {
            return;
        }

        Viererzweig zweig = wurzel.Kopie();
        zweig.Setzen(erster, 1);
        Tiefe(zweig, token, fortschritt);
        Interlocked.Add(ref knoten, zweig.Knoten);
    }

    private void Tiefe(Viererzweig z, CancellationToken token, Action<string>? fortschritt)
    {
        Zaehlen(z, fortschritt);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (z.Gedeckt == alle)
        {
            Blatt(z);
            return;
        }

        if (z.Km + Schranke(z) >= Volatile.Read(ref beste) || Kandidaten(z) is not List<int> kandidaten)
        {
            return;
        }

        foreach (int nummer in kandidaten)
        {
            Viererblock b = bloecke[nummer];
            if (z.Km + b.Km >= Volatile.Read(ref beste))
            {
                break;
            }

            z.Setzen(b, 1);
            Tiefe(z, token, fortschritt);
            z.Setzen(b, -1);
        }
    }

    private void Zaehlen(Viererzweig z, Action<string>? fortschritt)
    {
        z.Knoten++;
        if (z.Knoten < Meldetakt)
        {
            return;
        }

        long gesamt = Interlocked.Add(ref knoten, z.Knoten);
        z.Knoten = 0;
        long jetzt = uhr.ElapsedMilliseconds;
        long faellig = Interlocked.Read(ref naechsteMeldung);
        if (fortschritt is not null && jetzt >= faellig && Interlocked.CompareExchange(ref naechsteMeldung, jetzt + 500, faellig) == faellig)
        {
            double km = Volatile.Read(ref beste);
            string bisher = km < double.MaxValue ? string.Create(Deutsch, $", kürzester bisher {km:N0} km") : string.Empty;
            fortschritt(string.Create(Deutsch, $"Nur 4er-Spieltage: {gesamt:N0} Teilpläne geprüft{bisher} …"));
        }
    }

    /// <summary>Die passenden Spieltage für die offene Paarung mit den wenigsten; <c>null</c>, wenn eine keinen mehr hat.</summary>
    private List<int>? Kandidaten(Viererzweig z)
    {
        int bestes = -1;
        int wenigste = int.MaxValue;
        for (int p = 0; p < modell.Paare; p++)
        {
            if (((z.Gedeckt >> p) & 1) != 0)
            {
                continue;
            }

            int anzahl = Anzahl(z, p, wenigste);
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

        return bestes < 0 || wenigste == 0 ? null : jePaar[bestes].Where(b => Passt(z, bloecke[b])).ToList();
    }

    private int Anzahl(Viererzweig z, int paar, int grenze)
    {
        int anzahl = 0;
        int[] liste = jePaar[paar];
        for (int i = 0; i < liste.Length && anzahl < grenze; i++)
        {
            anzahl += Passt(z, bloecke[liste[i]]) ? 1 : 0;
        }

        return anzahl;
    }

    /// <summary>Untere Schranke der noch fehlenden Kilometer.</summary>
    private double Schranke(Viererzweig z) => Math.Max(GastSchranke(z), GastgeberSchranke(z));

    /// <summary>Jede Mannschaft fährt noch so oft zu einem 4er-Spieltag, wie ihr fehlt – mindestens zu den nächsten freien Gastgebern.</summary>
    private double GastSchranke(Viererzweig z)
    {
        double summe = 0;
        for (int t = 0; t < modell.N; t++)
        {
            int rest = gastSoll - z.Gast[t];
            for (int i = 0; rest > 0 && i < gastgeberNachNaehe[t].Length; i++)
            {
                int h = gastgeberNachNaehe[t][i];
                if (((z.Ausrichter >> h) & 1) == 0 && !z.Besucht[(t * modell.N) + h])
                {
                    summe += 2 * modell.Entfernung(t, h);
                    rest--;
                }
            }

            if (rest > 0)
            {
                return double.MaxValue;
            }
        }

        return summe;
    }

    /// <summary>Jeder Gastgeber ohne Spieltag bekommt noch drei Gäste – mindestens die nächsten, die noch fahren können.</summary>
    private double GastgeberSchranke(Viererzweig z)
    {
        double summe = 0;
        for (int h = 0; h < modell.N; h++)
        {
            if (((z.Ausrichter >> h) & 1) != 0)
            {
                continue;
            }

            int rest = GaesteJeSpieltag;
            for (int i = 0; rest > 0 && i < gaesteNachNaehe[h].Length; i++)
            {
                int t = gaesteNachNaehe[h][i];
                if (z.Gast[t] < gastSoll && !z.Besucht[(t * modell.N) + h])
                {
                    summe += 2 * modell.Entfernung(t, h);
                    rest--;
                }
            }

            if (rest > 0)
            {
                return double.MaxValue;
            }
        }

        return summe;
    }

    /// <summary>Ein vollständiger Plan: Sonntage verteilen; ist er kürzer als der bisher beste, gilt er.</summary>
    private void Blatt(Viererzweig z)
    {
        if (z.Km >= Volatile.Read(ref beste))
        {
            return;
        }

        List<Viererblock> alleBloecke = [.. z.Bloecke];
        int[]? tage = Sonntage(alleBloecke);
        if (tage is null)
        {
            return;
        }

        lock (sperre)
        {
            if (z.Km < beste)
            {
                Volatile.Write(ref beste, z.Km);
                Beste = alleBloecke.Select((b, i) => modell.Kandidat(b.Form, b.Mannschaften, tage[i])).ToList();
            }
        }
    }

    /// <summary>Verteilt die Spieltage auf Sonntage (jede Mannschaft höchstens einmal am Tag); <c>null</c>, wenn es nicht geht.</summary>
    private int[]? Sonntage(List<Viererblock> liste)
    {
        int[] reihenfolge = Enumerable.Range(0, liste.Count).OrderBy(i => BitOperations.PopCount(liste[i].Tage)).ToArray();
        var belegt = new int[modell.T];
        var tag = new int[liste.Count];
        return Zuordnen(0) ? tag : null;

        bool Zuordnen(int k)
        {
            if (k == reihenfolge.Length)
            {
                return true;
            }

            Viererblock b = liste[reihenfolge[k]];
            for (int d = 0; d < modell.T; d++)
            {
                if (((b.Tage >> d) & 1) != 0 && (belegt[d] & b.TeamMaske) == 0)
                {
                    belegt[d] |= b.TeamMaske;
                    tag[reihenfolge[k]] = d;
                    if (Zuordnen(k + 1))
                    {
                        return true;
                    }

                    belegt[d] &= ~b.TeamMaske;
                }
            }

            return false;
        }
    }
}
