// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Tabellen;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation;

/// <summary>
/// Baut die Excel-Arbeitsmappe eines Plans (neu, im Original nicht vorhanden): Übersicht, Spielplan und
/// Mannschaftspläne als filterbare Listen mit echten Datums- und Zeitwerten, dazu die Kostentabelle wie in der Ansicht.
/// </summary>
internal static class Tabellenexport
{
    private const int Kopfhintergrund = 0xE8E8E8;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Erzeugt die Arbeitsmappe.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="plan">Name der Planquelle.</param>
    /// <param name="erstellt">Zeitpunkt des Exports.</param>
    /// <param name="spiele">Die Spiele des Plans mit Hinweisen.</param>
    /// <param name="kennzahlen">Die Kennzahlen der Kostenansicht.</param>
    /// <param name="kosten">Die Kostentabelle.</param>
    /// <returns>Die Arbeitsmappe.</returns>
    internal static Arbeitsmappe Erstellen(
        Staffel staffel, string plan, DateTime erstellt, IReadOnlyList<Terminplanzeile> spiele, IReadOnlyList<Kennzahl> kennzahlen, Kostentabelle kosten)
    {
        string gesamtkosten = kennzahlen.Count > 0 ? kennzahlen[0].Wert.Text : string.Empty;
        List<Arbeitsblatt> blaetter =
        [
            Uebersicht(staffel, plan, erstellt, spiele, gesamtkosten),
            Spielplan(spiele),
            Mannschaftsplaene(spiele, staffel),
            Kosten(kennzahlen, kosten),
        ];
        return new Arbeitsmappe(blaetter);
    }

    private static Arbeitsblatt Uebersicht(Staffel staffel, string plan, DateTime erstellt, IReadOnlyList<Terminplanzeile> spiele, string gesamtkosten)
    {
        List<IReadOnlyList<Blattzelle>> zeilen =
        [
            [new Blattzelle(UeberViewModel.Programm + " – Export", Fett: true)],
            [],
            Paar("Staffel", staffel.Name),
            Paar("Plan", plan),
            Paar("Erstellt", erstellt.ToString("dd.MM.yyyy HH:mm", Deutsch)),
            [Blattzelle.Text("Mannschaften", fett: true), Blattzelle.Zahl(staffel.Mannschaften.Count)],
            [Blattzelle.Text("Spiele", fett: true), Blattzelle.Zahl(spiele.Count)],
            [Blattzelle.Text("davon ohne Termin", fett: true), Blattzelle.Zahl(spiele.Count(s => s.Zeitpunkt is null))],
            Paar("Gesamtkosten", gesamtkosten),
        ];
        return new Arbeitsblatt("Übersicht", zeilen);
    }

    private static Arbeitsblatt Spielplan(IReadOnlyList<Terminplanzeile> spiele)
    {
        var zeilen = new List<IReadOnlyList<Blattzelle>> { Kopf("Datum", "Tag", "Uhrzeit", "KW", "Runde", "Heim", "Gast", "Hinweis") };
        zeilen.AddRange(spiele.Select(Spielzeile));
        return new Arbeitsblatt("Spielplan", zeilen, Kopfzeile: 0);
    }

    private static Arbeitsblatt Mannschaftsplaene(IReadOnlyList<Terminplanzeile> spiele, Staffel staffel)
    {
        var zeilen = new List<IReadOnlyList<Blattzelle>> { Kopf("Mannschaft", "Datum", "Tag", "Uhrzeit", "H/A", "Gegner", "Heim", "Gast", "Hinweis") };
        foreach (string name in staffel.Mannschaften.Select(m => m.Name))
        {
            zeilen.AddRange(spiele.Where(z => z.Heim == name || z.Gast == name).Select(z => Mannschaftszeile(name, z)));
        }

        return new Arbeitsblatt("Mannschaftspläne", zeilen, Kopfzeile: 0);
    }

    private static Arbeitsblatt Kosten(IReadOnlyList<Kennzahl> kennzahlen, Kostentabelle tabelle)
    {
        var kopf = new List<string> { "Mannschaft" };
        kopf.AddRange(tabelle.Spalten.Select(s => s.Text));
        kopf.Add("Gesamt");
        kopf.Add("Meldungen");
        var zeilen = new List<IReadOnlyList<Blattzelle>> { Kopf(kopf.ToArray()) };
        foreach (Kostenzeile z in tabelle.Zeilen)
        {
            var zellen = new List<Blattzelle> { Blattzelle.Text(z.Mannschaft.Text) };
            zellen.AddRange(z.Zellen.Select(Feld));
            zellen.Add(Feld(z.Gesamt));
            zellen.Add(Blattzelle.Text(z.Meldungen.Replace(Environment.NewLine, "; ", StringComparison.Ordinal)));
            zeilen.Add(zellen);
        }

        var summe = new List<Blattzelle> { Blattzelle.Text("Summe", fett: true) };
        summe.AddRange(tabelle.Summen.Select(f => Feld(f) with { Fett = true }));
        summe.Add(Feld(tabelle.Gesamtsumme) with { Fett = true });
        zeilen.Add(summe);
        zeilen.Add([]);
        zeilen.AddRange(kennzahlen.Select(k => (IReadOnlyList<Blattzelle>)[Blattzelle.Text(k.Name, fett: true), Feld(k.Wert)]));
        return new Arbeitsblatt("Kosten", zeilen);
    }

    private static List<Blattzelle> Spielzeile(Terminplanzeile z) =>
    [
        Blattzelle.Datum(z.Zeitpunkt),
        Blattzelle.Text(Wochentag(z.Zeitpunkt)),
        Blattzelle.Uhrzeit(z.Zeitpunkt),
        z.Zeitpunkt is null ? Blattzelle.Leer : Blattzelle.Zahl(z.Kalenderwoche),
        Blattzelle.Zahl(z.Runde),
        Blattzelle.Text(z.Heim),
        Blattzelle.Text(z.Gast),
        Blattzelle.Text(Hinweis(z)),
    ];

    private static List<Blattzelle> Mannschaftszeile(string name, Terminplanzeile z) =>
    [
        Blattzelle.Text(name),
        Blattzelle.Datum(z.Zeitpunkt),
        Blattzelle.Text(Wochentag(z.Zeitpunkt)),
        Blattzelle.Uhrzeit(z.Zeitpunkt),
        Blattzelle.Text(z.Heim == name ? "H" : "A"),
        Blattzelle.Text(z.Heim == name ? z.Gast : z.Heim),
        Blattzelle.Text(z.Heim),
        Blattzelle.Text(z.Gast),
        Blattzelle.Text(Hinweis(z)),
    ];

    private static string Hinweis(Terminplanzeile z) => z.Zeitpunkt is null ? Verbinden("nicht terminiert", z.Hinweis) : z.Hinweis;

    private static Blattzelle Feld(Kostenfeld feld)
    {
        string text = feld.Markierung.Length > 0 ? feld.Text + " " + feld.Markierung : feld.Text;
        if (feld.Anteil <= 0)
        {
            return Blattzelle.Text(text);
        }

        return new Blattzelle(text, Hintergrund: Rgb(Druckbericht.Hinterlegung(feld.Anteil)), Schriftfarbe: Rgb(Druckbericht.Hinterlegungsschrift(feld.Anteil)));
    }

    private static List<Blattzelle> Kopf(params string[] titel) =>
        titel.Select(t => new Blattzelle(t, Fett: true, Hintergrund: Kopfhintergrund)).ToList();

    private static List<Blattzelle> Paar(string name, string wert) => [Blattzelle.Text(name, fett: true), Blattzelle.Text(wert)];

    private static string Wochentag(DateTime? zeitpunkt) => zeitpunkt is DateTime t ? t.ToString("ddd", Deutsch) : string.Empty;

    private static int Rgb(Farbe farbe) => (farbe.R << 16) | (farbe.G << 8) | farbe.B;

    private static string Verbinden(string a, string b) => b.Length == 0 ? a : a + ", " + b;
}
