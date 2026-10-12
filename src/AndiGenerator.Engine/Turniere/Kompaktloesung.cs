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
        Verstoesse = Turniere.Sum(k => k.Verstoesse);
        var ausgerichtet = new int[modell.N];
        var heimspiele = new int[modell.N];
        foreach (Kompaktkandidat k in Turniere)
        {
            heimspiele[k.Ausrichter] += k.IstEinzel ? 1 : 2;
            ausgerichtet[k.Ausrichter] += k.IstEinzel ? 0 : 1;
        }

        OhneHeimspieltag = ausgerichtet.Count(a => a == 0);
        Vierer = Turniere.Count(k => k.Form == Turnierform.Vierer);
        Dreier = Turniere.Count(k => k.Form == Turnierform.Dreier);
        Einzel = Turniere.Count(k => k.IstEinzel);
        Heimspiele = modell.N == 0 ? 0 : heimspiele.Max() - heimspiele.Min();
        (Abstand, SummeAbstand, Gemeinsam) = Stand();
    }

    public Kompaktkandidat[] Turniere { get; }

    public double Km { get; }

    public int Verstoesse { get; }

    public int OhneHeimspieltag { get; }

    public int Heimspiele { get; }

    public int Vierer { get; }

    public int Dreier { get; }

    public int Einzel { get; }

    /// <summary>Holt den größten Unterschied gespielter Spiele nach einem Spieltag.</summary>
    public int Abstand { get; }

    /// <summary>Holt die Summe der Unterschiede nach allen Spieltagen (feinere Wertung des Stands).</summary>
    public int SummeAbstand { get; }

    /// <summary>Holt die größte Zahl von Mannschaften an einem Tag.</summary>
    public int Gemeinsam { get; }

    /// <summary>Holt einen Text, der den Plan eindeutig beschreibt (zum Erkennen doppelter Pläne).</summary>
    public string Schluessel => schluessel ??= string.Join(
        ';',
        Turniere.Select(k => string.Create(CultureInfo.InvariantCulture, $"{k.Tag}:{(int)k.Form}:{string.Join(',', k.Mannschaften)}")).Order(StringComparer.Ordinal));

    /// <summary>Die Kennzahlen für die Ausgabe.</summary>
    public Kompaktkennzahlen Kennzahlen() => new(Km, Abstand, Gemeinsam, Verstoesse, OhneHeimspieltag, Heimspiele, Vierer, Dreier, Einzel);

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
