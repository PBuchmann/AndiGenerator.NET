// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein vollständiger Plan der Suche mit seinen Kennzahlen.</summary>
internal sealed class Kompaktloesung
{
    private readonly Kompaktmodell modell;
    private string? schluessel;

    /// <summary>Bewertet einen Plan.</summary>
    public Kompaktloesung(Kompaktmodell modell, IReadOnlyList<Kompaktkandidat> turniere)
    {
        this.modell = modell;
        Turniere = turniere.ToArray();
        Km = Turniere.Sum(k => k.Km);
        EinzelKm = Turniere.Where(k => k.IstEinzel).Sum(k => k.Km);
        Verstoesse = Turniere.Sum(k => k.Verstoesse);
        var ausgerichtet = new int[modell.N];
        var heimspiele = new int[modell.N];
        foreach (Kompaktkandidat k in Turniere)
        {
            heimspiele[k.Ausrichter] += k.IstEinzel ? 1 : 2;
            ausgerichtet[k.Ausrichter] += k.IstEinzel ? 0 : 1;
        }

        OhneHeimspieltag = ausgerichtet.Count(a => a == 0);
        OhneHeimspiel = heimspiele.Count(h => h == 0);
        Vierer = Turniere.Count(k => k.Form == Turnierform.Vierer);
        Dreier = Turniere.Count(k => k.Form == Turnierform.Dreier);
        Einzel = Turniere.Count(k => k.IstEinzel);
        Heimspiele = modell.N == 0 ? 0 : heimspiele.Max() - heimspiele.Min();
        (Abstand, SummeAbstand, Gemeinsam) = Stand();
        (GruppenFehlen, GruppenFrueh, GruppenTage) = Gruppen();
        var kmJe = new double[modell.N];
        foreach (Kompaktkandidat k in Turniere)
        {
            for (int i = 1; i < k.Mannschaften.Length; i++)
            {
                kmJe[k.Mannschaften[i]] += 2 * modell.Entfernung(k.Mannschaften[i], k.Ausrichter);
            }
        }

        KmSpanne = modell.N == 0 ? 0 : kmJe.Max() - kmJe.Min();
    }

    public Kompaktkandidat[] Turniere { get; }

    public double Km { get; }

    /// <summary>Holt die Kilometer der Einzelbegegnungen (Gast hin und zurück).</summary>
    public double EinzelKm { get; }

    /// <summary>
    /// Holt die Kilometer für die Bewertung: Fahrten zu Einzelbegegnungen zählen doppelt (Peter, 13.10.2026), damit lange
    /// Einzelfahrten – eine Mannschaft fährt weit für ein einziges Spiel – vermieden werden.
    /// </summary>
    public double Bewertungskm => Km + EinzelKm;

    public int Verstoesse { get; }

    public int OhneHeimspieltag { get; }

    public int Heimspiele { get; }

    /// <summary>Holt, wie viele Mannschaften gar kein Heimspiel haben (weder Spieltag noch Einzelbegegnung zuhause).</summary>
    public int OhneHeimspiel { get; }

    public int Vierer { get; }

    public int Dreier { get; }

    public int Einzel { get; }

    /// <summary>Holt den größten Unterschied gespielter Spiele nach einem Spieltag.</summary>
    public int Abstand { get; }

    /// <summary>Holt die Summe der Unterschiede nach allen Spieltagen (feinere Wertung des Stands).</summary>
    public int SummeAbstand { get; }

    /// <summary>Holt die größte Zahl von Mannschaften an einem Tag.</summary>
    public int Gemeinsam { get; }

    /// <summary>Holt den Unterschied zwischen der Mannschaft mit den meisten und der mit den wenigsten Kilometern.</summary>
    public double KmSpanne { get; }

    /// <summary>
    /// Holt, wie viele der Gruppen (Spitze, Keller) keinen eigenen Spieltag haben: weder einen 4er-Spieltag der ganzen Gruppe
    /// noch einen 3er-Spieltag ihrer drei stärksten bzw. schwächsten Mannschaften.
    /// </summary>
    public int GruppenFehlen { get; }

    /// <summary>Holt, wie früh die Gruppenspieltage liegen: je Gruppe 0 = am letzten, 1 = am ersten Spieltag des Plans.</summary>
    public double GruppenFrueh { get; }

    /// <summary>
    /// Holt, wie viele Gruppen einen eigenen Spieltag haben: einen 4er-Spieltag der ganzen Gruppe oder wenigstens einen
    /// 3er-Spieltag ihrer drei stärksten bzw. schwächsten Mannschaften.
    /// </summary>
    public int GruppenTage { get; }

    /// <summary>Holt einen Text, der den Plan eindeutig beschreibt (zum Erkennen doppelter Pläne).</summary>
    public string Schluessel => schluessel ??= string.Join(
        ';',
        Turniere.Select(k => string.Create(CultureInfo.InvariantCulture, $"{k.Tag}:{(int)k.Form}:{string.Join(',', k.Mannschaften)}")).Order(StringComparer.Ordinal));

    /// <summary>Die Kennzahlen für die Ausgabe.</summary>
    public Kompaktkennzahlen Kennzahlen() => new(Km, Abstand, Gemeinsam, Verstoesse, OhneHeimspieltag, Heimspiele, Vierer, Dreier, Einzel, KmSpanne) { EinzelKm = EinzelKm };

    /// <summary>Die Spieltage mit Namen, nach Datum.</summary>
    public List<Planturnier> Planturniere() => Turniere
        .OrderBy(k => k.Tag)
        .ThenBy(k => k.IstEinzel)
        .ThenBy(k => modell.Namen[k.Ausrichter], StringComparer.Ordinal)
        .Select(k => new Planturnier(modell.Tage[k.Tag], k.Form, k.Mannschaften.Select(m => modell.Namen[m]).ToList()))
        .ToList();

    private static void Zaehlen(Kompaktkandidat k, int[] gespielt)
    {
        foreach (int m in k.Mannschaften)
        {
            gespielt[m] += k.IstEinzel ? 1 : 2;
        }
    }

    private (int Fehlen, double Frueh, int Tage) Gruppen()
    {
        if (modell.Gruppen.Count == 0 || Turniere.Length == 0)
        {
            return (0, 0, 0);
        }

        int erster = Turniere.Min(k => k.Tag);
        int letzter = Turniere.Max(k => k.Tag);
        int fehlen = 0;
        int tage = 0;
        double frueh = 0;
        foreach (int[] g in modell.Gruppen)
        {
            // Ein 4er-Spieltag der Gruppe (die ersten beiden gegeneinander) oder ein 3er-Spieltag ihrer ersten drei (Peter,
            // 13.10.2026: in Plänen aus 3er-Spieltagen bilden die drei Stärksten bzw. Schwächsten die Gruppe).
            Kompaktkandidat? k = Array.Find(Turniere, x => x.Form == Turnierform.Vierer && Kompaktoptimierung.IstGruppentag(x, g))
                ?? Array.Find(Turniere, x => Kompaktoptimierung.IstGruppentag(x, g));
            if (k is null)
            {
                fehlen++;
                continue;
            }

            tage++;
            if (letzter > erster)
            {
                frueh += (double)(letzter - k.Tag) / (letzter - erster);
            }
        }

        return (fehlen, frueh, tage);
    }

    private (int Abstand, int Summe, int Gemeinsam) Stand()
    {
        var gespielt = new int[modell.N];
        var amTag = new int[modell.T];
        var gespieltAmTag = new bool[modell.T];
        foreach (Kompaktkandidat k in Turniere)
        {
            amTag[k.Tag] += k.Mannschaften.Length;
            gespieltAmTag[k.Tag] = true;
        }

        int abstand = 0;
        int summe = 0;
        for (int tag = 0; tag < modell.T; tag++)
        {
            if (!gespieltAmTag[tag])
            {
                continue;
            }

            foreach (Kompaktkandidat k in Turniere.Where(x => x.Tag == tag))
            {
                Zaehlen(k, gespielt);
            }

            int unterschied = gespielt.Max() - gespielt.Min();
            abstand = Math.Max(abstand, unterschied);
            summe += unterschied;
        }

        return (abstand, summe, amTag.DefaultIfEmpty(0).Max());
    }
}
