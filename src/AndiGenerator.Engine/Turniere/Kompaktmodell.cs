// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Aufbereitete Aufgabe des Kompaktplaners: Mannschaften, Tage und Paarungen als Nummern, Meldungen als Tabellen und alle
/// möglichen Spieltage (Dreier, Vierer, bei gerader Mannschaftszahl Einzelbegegnungen) je Paarung, ohne Verstöße zuerst.
/// </summary>
internal sealed class Kompaktmodell
{
    /// <summary>Höchstzahl der Verstöße gegen die Meldungen, die ein einzelner Spieltag haben darf.</summary>
    public const int MaxVerstoesseJeSpieltag = 2;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly double[] km;
    private readonly bool[] heim;
    private readonly bool[] auswaerts;
    private readonly bool[] zweiTische;
    private readonly int[] paarIndex;
    private readonly int[] wunschGastgeber;
    private readonly HashSet<Kompaktkandidat> fest = [];
    private readonly List<string> hinweise = [];

    /// <summary>Bereitet die Aufgabe auf.</summary>
    public Kompaktmodell(Kompaktaufgabe aufgabe)
    {
        Namen = aufgabe.Mannschaften.Distinct(StringComparer.Ordinal).ToArray();
        N = Namen.Length;
        Tage = aufgabe.Tage.Distinct().Order().ToArray();
        T = Tage.Length;
        km = new double[N * N];
        heim = new bool[N * T];
        auswaerts = new bool[N * T];
        zweiTische = new bool[N];
        paarIndex = new int[N * N];
        Paare = PaareNummerieren();
        EntfernungenLesen(aufgabe.Entfernung);
        MeldungenLesen(aufgabe.Meldungen);
        wunschGastgeber = WuenscheLesen(aufgabe.Fahrtwuensche);
        var kandidaten = new List<Kompaktkandidat>();
        for (int h = 0; h < N; h++)
        {
            DreierErzeugen(h, kandidaten);
            ViererErzeugen(h, kandidaten);
            EinzelErzeugen(h, kandidaten);
        }

        JePaar = Verteilen(kandidaten);
        JePaarOhneDreier = Verteilen(kandidaten.Where(k => k.Form != Turnierform.Dreier).ToList());
        KmSkala = Math.Max(1, kandidaten.Select(k => k.Km).DefaultIfEmpty(0).Average());
        Fest = FesteLesen(aufgabe.Festgehalten);

        // Bei gerader Zahl bleibt je Mannschaft eine Einzelbegegnung; jeder Dreier oder Vierer deckt mindestens drei
        // Paarungen. Bei wenigen Mannschaften (z. B. 5 oder 6) reichen die Spieltage nicht für einen Heimspieltag je Mannschaft.
        int turnierpaare = Paare - (N % 2 == 0 ? N / 2 : 0);
        MindestensOhneAusrichtung = Math.Max(0, N - (turnierpaare / 3));

        // Nur aus 4er-Spieltagen (Peter, 13.10.2026: muss gefunden werden, wenn es geht) – lohnt sich nur, wenn die Paarungen
        // genau aufgehen und trotzdem jede Mannschaft ausrichten kann (z. B. 9 oder 10 Mannschaften, nicht 8).
        NurViererMoeglich = turnierpaare > 0 && turnierpaare % 4 == 0 && N - (turnierpaare / 4) <= MindestensOhneAusrichtung;

        // Vierer bevorzugen nur, wenn genug Paarungen für einen 4er-Spieltag je Mannschaft da sind; bei 6 bis 8 Mannschaften
        // braucht es 3er-Spieltage, damit möglichst jede ausrichten kann.
        ViererBevorzugen = turnierpaare >= 4 * N;
    }

    public string[] Namen { get; }

    public int N { get; }

    public DateOnly[] Tage { get; }

    public int T { get; }

    public int Paare { get; }

    /// <summary>Holt je Paarung die möglichen Spieltage, nach Verstößen aufsteigend.</summary>
    public Kompaktkandidat[][] JePaar { get; }

    /// <summary>Holt je Paarung die möglichen Vierer und Einzelbegegnungen (Suche nur mit 4er-Spieltagen).</summary>
    public Kompaktkandidat[][] JePaarOhneDreier { get; }

    /// <summary>Holt einen Wert, der angibt, ob ein Plan nur aus 4er-Spieltagen (und Einzelbegegnungen) rechnerisch möglich ist.</summary>
    public bool NurViererMoeglich { get; }

    /// <summary>Holt einen Wert, der angibt, ob die Suche 4er-Spieltage bevorzugt (ab 9 Mannschaften).</summary>
    public bool ViererBevorzugen { get; }

    /// <summary>Holt, wie viele Mannschaften rechnerisch mindestens ohne Heimspieltag bleiben.</summary>
    public int MindestensOhneAusrichtung { get; }

    /// <summary>Holt die festgehaltenen Spieltage.</summary>
    public IReadOnlyList<Kompaktkandidat> Fest { get; }

    /// <summary>Holt die mittleren Kilometer eines Spieltags (Maßstab für die Zufallsstreuung der Suche).</summary>
    public double KmSkala { get; }

    /// <summary>Holt Hinweise zu den Eingaben (fehlende Meldungen, unbrauchbare Fahrtwünsche oder festgehaltene Spieltage).</summary>
    public IReadOnlyList<string> Hinweise => hinweise;

    /// <summary>Ob ein Spieltag festgehalten ist.</summary>
    public bool IstFest(Kompaktkandidat kandidat) => fest.Contains(kandidat);

    /// <summary>Verstöße gegen die Meldungen: Ausrichter kann nicht zuhause (beim Vierer: oder nicht an zwei Tischen), Gäste nicht auswärts.</summary>
    public int Verstoesse(Turnierform form, int[] mannschaften, int tag)
    {
        int ausrichter = mannschaften[0];
        bool kannAusrichten = heim[(ausrichter * T) + tag] && (form != Turnierform.Vierer || zweiTische[ausrichter]);
        int gaeste = 0;
        for (int i = 1; i < mannschaften.Length; i++)
        {
            gaeste += auswaerts[(mannschaften[i] * T) + tag] ? 0 : 1;
        }

        return (kannAusrichten ? 0 : 1) + gaeste;
    }

    /// <summary>Ein Spieltag mit allen abgeleiteten Angaben.</summary>
    public Kompaktkandidat Kandidat(Turnierform form, int[] mannschaften, int tag) =>
        new(form, mannschaften, tag, PaareVon(form, mannschaften), Strecke(mannschaften), Verstoesse(form, mannschaften, tag));

    /// <summary>Ob ein Spieltag die Fahrtwünsche einhält: Jede gewünschte Paarung findet in der Halle des Gastgebers statt.</summary>
    public bool Wunschgerecht(Kompaktkandidat kandidat) => PasstZuWuenschen(kandidat.Mannschaften, kandidat.Paare);

    /// <summary>Ob ein Spieltag (Mannschaften, Paarungen) die Fahrtwünsche einhält.</summary>
    public bool Wunschgerecht(int[] mannschaften, int[] paare) => PasstZuWuenschen(mannschaften, paare);

    /// <summary>Die Nummern der Paarungen eines Spieltags.</summary>
    public int[] PaareFuer(Turnierform form, int[] mannschaften) => PaareVon(form, mannschaften);

    /// <summary>Kilometer der Gäste zur Halle des Ausrichters, hin und zurück.</summary>
    public double StreckeFuer(int[] mannschaften) => Strecke(mannschaften);

    /// <summary>Einfache Entfernung zwischen den Hallen zweier Mannschaften.</summary>
    public double Entfernung(int von, int nach) => km[(von * N) + nach];

    private Kompaktkandidat[][] Verteilen(List<Kompaktkandidat> kandidaten)
    {
        var je = new List<Kompaktkandidat>[Paare];
        for (int p = 0; p < Paare; p++)
        {
            je[p] = [];
        }

        foreach (Kompaktkandidat k in kandidaten)
        {
            foreach (int p in k.Paare)
            {
                je[p].Add(k);
            }
        }

        return je.Select(l => l.OrderBy(k => k.Verstoesse).ToArray()).ToArray();
    }

    private int PaareNummerieren()
    {
        int p = 0;
        for (int a = 0; a < N; a++)
        {
            for (int b = a + 1; b < N; b++)
            {
                paarIndex[(a * N) + b] = p;
                paarIndex[(b * N) + a] = p;
                p++;
            }
        }

        return p;
    }

    private int Paar(int a, int b) => paarIndex[(a * N) + b];

    private void EntfernungenLesen(Func<string, string, double> entfernung)
    {
        for (int a = 0; a < N; a++)
        {
            for (int b = 0; b < N; b++)
            {
                km[(a * N) + b] = a == b ? 0 : Math.Max(0, entfernung(Namen[a], Namen[b]));
            }
        }
    }

    private void MeldungenLesen(IReadOnlyList<Turniermeldung> meldungen)
    {
        for (int i = 0; i < N; i++)
        {
            // Ohne Meldung gilt jeder Sonntag als möglich (das meldet schon die Prüfung der Meldungen).
            Turniermeldung? meldung = Turnierpruefung.Meldung(Namen[i], meldungen);
            zweiTische[i] = meldung?.ZweiTische ?? true;
            for (int d = 0; d < T; d++)
            {
                Turnierbereitschaft b = meldung?.An(Tage[d]) ?? (Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts);
                heim[(i * T) + d] = b.HasFlag(Turnierbereitschaft.Heim);
                auswaerts[(i * T) + d] = b.HasFlag(Turnierbereitschaft.Auswaerts);
            }
        }
    }

    private int[] WuenscheLesen(IReadOnlyList<Fahrtwunsch> wuensche)
    {
        int[] gastgeber = Enumerable.Repeat(-1, Paare).ToArray();
        foreach (Fahrtwunsch w in wuensche)
        {
            int gast = Array.IndexOf(Namen, w.Gast);
            int ziel = Array.IndexOf(Namen, w.Gastgeber);
            if (gast < 0 || ziel < 0 || gast == ziel)
            {
                hinweise.Add($"Fahrtwunsch {w.Gast} → {w.Gastgeber}: Mannschaft nicht in der Staffel – übergangen.");
                continue;
            }

            int p = Paar(gast, ziel);
            if (gastgeber[p] >= 0 && gastgeber[p] != ziel)
            {
                hinweise.Add($"Fahrtwunsch {w.Gast} → {w.Gastgeber} widerspricht einem anderen Wunsch für diese Paarung – übergangen.");
                continue;
            }

            gastgeber[p] = ziel;
        }

        return gastgeber;
    }

    private bool PasstZuWuenschen(int[] mannschaften, int[] paare) =>
        Array.TrueForAll(paare, p => wunschGastgeber[p] < 0 || wunschGastgeber[p] == mannschaften[0]);

    private int[] PaareVon(Turnierform form, int[] m) => form switch
    {
        Turnierform.Vierer => [Paar(m[0], m[2]), Paar(m[0], m[3]), Paar(m[1], m[2]), Paar(m[1], m[3])],
        Turnierform.Dreier => [Paar(m[0], m[1]), Paar(m[0], m[2]), Paar(m[1], m[2])],
        _ => [Paar(m[0], m[1])],
    };

    /// <summary>Kilometer der Gäste zur Halle des Ausrichters, hin und zurück.</summary>
    private double Strecke(int[] mannschaften)
    {
        double summe = 0;
        for (int i = 1; i < mannschaften.Length; i++)
        {
            summe += 2 * km[(mannschaften[i] * N) + mannschaften[0]];
        }

        return summe;
    }

    private void Hinzufuegen(Turnierform form, int[] mannschaften, List<Kompaktkandidat> kandidaten)
    {
        int[] paare = PaareVon(form, mannschaften);
        if (!PasstZuWuenschen(mannschaften, paare))
        {
            return;
        }

        double strecke = Strecke(mannschaften);
        for (int d = 0; d < T; d++)
        {
            int verstoesse = Verstoesse(form, mannschaften, d);
            if (verstoesse <= MaxVerstoesseJeSpieltag)
            {
                kandidaten.Add(new Kompaktkandidat(form, mannschaften, d, paare, strecke, verstoesse));
            }
        }
    }

    private void DreierErzeugen(int h, List<Kompaktkandidat> kandidaten)
    {
        for (int a = 0; a < N; a++)
        {
            for (int b = a + 1; b < N; b++)
            {
                if (a != h && b != h)
                {
                    Hinzufuegen(Turnierform.Dreier, [h, a, b], kandidaten);
                }
            }
        }
    }

    /// <summary>Vierer: Ausrichter h und Partner b spielen gegen c und e.</summary>
    private void ViererErzeugen(int h, List<Kompaktkandidat> kandidaten)
    {
        for (int b = 0; b < N; b++)
        {
            if (b == h)
            {
                continue;
            }

            for (int c = 0; c < N; c++)
            {
                for (int e = c + 1; e < N; e++)
                {
                    if (c != h && c != b && e != h && e != b)
                    {
                        Hinzufuegen(Turnierform.Vierer, [h, b, c, e], kandidaten);
                    }
                }
            }
        }
    }

    /// <summary>Einzelbegegnungen nur bei gerader Mannschaftszahl (dann bleibt für jede Mannschaft genau eine übrig).</summary>
    private void EinzelErzeugen(int h, List<Kompaktkandidat> kandidaten)
    {
        if (N % 2 != 0)
        {
            return;
        }

        for (int a = 0; a < N; a++)
        {
            if (a != h)
            {
                Hinzufuegen(Turnierform.Einzel, [h, a], kandidaten);
            }
        }
    }

    private List<Kompaktkandidat> FesteLesen(IReadOnlyList<Planturnier> festgehalten)
    {
        var liste = new List<Kompaktkandidat>();
        var gedeckt = new bool[Paare];
        var belegt = new bool[N * T];
        foreach (Planturnier turnier in festgehalten)
        {
            Kompaktkandidat? k = Umsetzen(turnier);
            if (k is null || !Frei(k, gedeckt, belegt))
            {
                hinweise.Add($"Festgehaltener Spieltag am {turnier.Tag.ToString("dd.MM.yyyy", Deutsch)} ({string.Join(", ", turnier.Mannschaften)}) passt nicht (mehr) – übergangen.");
                continue;
            }

            foreach (int p in k.Paare)
            {
                gedeckt[p] = true;
            }

            foreach (int m in k.Mannschaften)
            {
                belegt[(m * T) + k.Tag] = true;
            }

            liste.Add(k);
            fest.Add(k);
        }

        return liste;
    }

    private bool Frei(Kompaktkandidat k, bool[] gedeckt, bool[] belegt) =>
        !Array.Exists(k.Paare, p => gedeckt[p]) && !Array.Exists(k.Mannschaften, m => belegt[(m * T) + k.Tag]);

    private Kompaktkandidat? Umsetzen(Planturnier turnier)
    {
        int[] mannschaften = turnier.Mannschaften.Select(m => Array.IndexOf(Namen, m)).ToArray();
        int tag = Array.IndexOf(Tage, turnier.Tag);
        int anzahl = turnier.Form switch
        {
            Turnierform.Vierer => 4,
            Turnierform.Dreier => 3,
            Turnierform.Einzel => 2,
            _ => 0,
        };
        bool gueltig = tag >= 0 && anzahl > 0 && mannschaften.Length == anzahl && Array.TrueForAll(mannschaften, m => m >= 0)
            && mannschaften.Distinct().Count() == anzahl;
        return gueltig ? Kandidat(turnier.Form, mannschaften, tag) : null;
    }
}
