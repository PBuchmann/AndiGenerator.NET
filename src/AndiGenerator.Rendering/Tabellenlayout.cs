// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Gemessene Spalten einer Tabelle: jede Spalte so breit wie ihr längster Eintrag. Passt die Tabelle nicht in die
/// Breite, wird sie verkleinert; mit <see cref="Tabelle.LetzteSpalteUmbrechen"/> bricht stattdessen die letzte Spalte um.
/// </summary>
internal sealed class Tabellenlayout
{
    private const double Spaltenabstand = 8;
    private const double MindestbreiteLetzteSpalte = 60;

    private readonly IZeichenflaeche messung;

    public Tabellenlayout(Tabelle tabelle, double verfuegbar, IZeichenflaeche messung)
    {
        this.messung = messung;
        Kopf = tabelle.Kopf;
        int anzahl = Math.Max(tabelle.Kopf?.Count ?? 0, tabelle.Zeilen.Where(z => z.Art == Zeilenart.Normal).Select(z => z.Zellen.Count).DefaultIfEmpty(0).Max());
        var breiten = new double[anzahl];
        for (int s = 0; s < anzahl; s++)
        {
            double kopf = tabelle.Kopf is { } k && s < k.Count ? KopfBreite(k[s], s, tabelle, messung) : 0;
            double inhalt = tabelle.Zeilen
                .Where(z => z.Art == Zeilenart.Normal && s < z.Zellen.Count)
                .Select(z => messung.TextBreite(z.Zellen[s].Text, tabelle.Groesse, z.Fett))
                .DefaultIfEmpty(0)
                .Max();
            breiten[s] = Math.Max(kopf, inhalt) + (s < anzahl - 1 ? Spaltenabstand : 0);
        }

        double skala = 1;
        if (tabelle.LetzteSpalteUmbrechen && anzahl > 0)
        {
            double vorne = breiten.Take(anzahl - 1).Sum();
            skala = Math.Min(1, (verfuegbar - MindestbreiteLetzteSpalte) / Math.Max(vorne, 1));
            for (int s = 0; s < anzahl - 1; s++)
            {
                breiten[s] *= skala;
            }

            breiten[anzahl - 1] = Math.Max(verfuegbar - (vorne * skala), MindestbreiteLetzteSpalte);
            Umbrechend = true;
        }
        else if (breiten.Sum() > verfuegbar)
        {
            skala = verfuegbar / breiten.Sum();
            for (int s = 0; s < anzahl; s++)
            {
                breiten[s] *= skala;
            }
        }

        Groesse = tabelle.Groesse * skala;
        Zeilenhoehe = Groesse * Seitenumbruch.Zeilenfaktor;
        Abstand = Spaltenabstand * skala;
        Matrix = tabelle.Matrix;
        KopfHoehe = KopfhoeheMessen(tabelle, Groesse, Zeilenhoehe, messung);
        var spalten = new List<Tabellenspalte>();
        double x = 0;
        foreach (double breite in breiten)
        {
            spalten.Add(new Tabellenspalte(x, breite));
            x += breite;
        }

        Spalten = spalten;
        Breite = Math.Min(x, verfuegbar);
    }

    public IReadOnlyList<string>? Kopf { get; }

    public IReadOnlyList<Tabellenspalte> Spalten { get; }

    public double Groesse { get; }

    public double Zeilenhoehe { get; }

    public double Breite { get; }

    /// <summary>Holt die Höhe des Tabellenkopfs samt Linie (senkrechte Köpfe: so hoch wie der längste).</summary>
    public double KopfHoehe { get; }

    /// <summary>Holt den Abstand rechts in jeder Spalte außer der letzten.</summary>
    public double Abstand { get; }

    /// <summary>Holt, ob die Tabelle als Matrix dargestellt wird (<see cref="Tabelle.Matrix"/>).</summary>
    public bool Matrix { get; }

    private bool Umbrechend { get; }

    public double Hoehe(Tabellenzeile zeile) => zeile.Art switch
    {
        Zeilenart.Abstand => Zeilenhoehe / 2,
        Zeilenart.Ueberschrift => (Zeilenhoehe * 0.3) + (Groesse * 1.15 * Seitenumbruch.Zeilenfaktor),
        _ => Zeilenhoehe * Math.Max(1, zeile.Zellen.Select((z, s) => Zeilen(z, s, zeile.Fett).Count).DefaultIfEmpty(1).Max()),
    };

    /// <summary>Holt, ob ein Spaltenkopf senkrecht steht.</summary>
    /// <param name="spalte">Die Spalte.</param>
    /// <returns><c>true</c> in einer Matrix ab der zweiten Spalte.</returns>
    public bool Senkrecht(int spalte) => Matrix && spalte > 0;

    /// <summary>Holt die Breite des Inhalts einer Spalte (ohne den Abstand zur nächsten).</summary>
    /// <param name="spalte">Die Spalte.</param>
    /// <returns>Die Breite.</returns>
    public double Inhaltsbreite(int spalte) => Spalten[spalte].Breite - (spalte < Spalten.Count - 1 ? Abstand : 0);

    public IReadOnlyList<string> Zeilen(Tabellenzelle zelle, int spalte, bool fett) =>
        Umbrechend && spalte == Spalten.Count - 1
            ? Seitenumbruch.Umbrechen(zelle.Text, Spalten[spalte].Breite, Groesse, fett, messung)
            : [zelle.Text];

    private static double KopfBreite(string text, int spalte, Tabelle tabelle, IZeichenflaeche messung) =>
        tabelle.Matrix && spalte > 0 ? tabelle.Groesse * 1.6 : messung.TextBreite(text, tabelle.Groesse, true);

    private static double KopfhoeheMessen(Tabelle tabelle, double groesse, double zeilenhoehe, IZeichenflaeche messung)
    {
        if (tabelle.Kopf is not { } kopf)
        {
            return 0;
        }

        if (!tabelle.Matrix)
        {
            return zeilenhoehe + 1;
        }

        double laengster = kopf.Skip(1).Select(t => messung.TextBreite(t, groesse, false)).DefaultIfEmpty(0).Max();
        return Math.Max(zeilenhoehe, laengster + 6) + 1;
    }
}
