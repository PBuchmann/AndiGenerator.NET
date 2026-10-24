// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Rendering;

/// <summary>Das Diagramm „Ausrichter und Spielorte“ für Kompaktspieltage (nicht im Original).</summary>
public sealed partial class Diagrammzeichner
{
    private const double Kartenbreite = 210;
    private const double Kartenhoehe = 34;
    private const double Kartenabstand = 8;
    private const double Zeilenabstand = 46;
    private const double Kartenrundung = 7;

    private static readonly Farbe Kartenrand = new(0xC9, 0xCE, 0xD6);
    private static readonly Farbe Blasstext = new(0xDC, 0xE7, 0xF5);

    private static bool Spielt(Ausrichtertag t, string name) => t.Spiele.Any(s => s.Heim == name || s.Gast == name);

    private static string Hinweistext(Ausrichtertag t)
    {
        var zeilen = new List<string>
        {
            t.Tag.ToString("dddd, dd.MM.yyyy", Deutsch),
            "Ausrichter: " + (t.Ausrichter ?? "offen"),
        };
        zeilen.AddRange(t.Spiele.Select(s => "   " + s.Heim + " – " + s.Gast));
        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>Kürzt einen Text auf eine Breite (geschätzt) mit „…“.</summary>
    private static string Kuerzen(string text, double breite, double groesse)
    {
        if (Schaetzen(text, groesse) <= breite)
        {
            return text;
        }

        int zeichen = Math.Max(1, (int)(breite / (groesse * 0.58)) - 1);
        return string.Concat(text.AsSpan(0, Math.Min(zeichen, text.Length)), "…");
    }

    /// <summary>Die Rahmen beim Überfahren: um die Karten aller Mannschaften, die an diesem Tag zusammen spielen.</summary>
    private static List<Diagrammmarke> Tagesrahmen(Ausrichtertag t, Dictionary<(DateOnly Tag, string Name), (double X, double Y)> karten) =>
        t.Spiele
            .SelectMany(s => new[] { s.Heim, s.Gast })
            .Distinct()
            .Where(n => karten.ContainsKey((t.Tag, n)))
            .Select(n => karten[(t.Tag, n)])
            .Select(k => new Diagrammmarke(k.X + (Kartenbreite / 2), k.Y + (Kartenhoehe / 2), Kartenrundung + 3, Kartenbreite + 6, Kartenhoehe + 6))
            .ToList();

    /// <summary>
    /// Je Mannschaft eine Karte je Spieltag: blau, wenn sie ausrichtet (eigene Halle), sonst die Halle des Ausrichters – es
    /// zählt, wo gespielt wird, nicht das Heimrecht laut Plan. Rechts, wie oft sie ausrichtet. Beim Überfahren einer Karte
    /// werden die Karten aller Mannschaften dieses Spieltags umrahmt.
    /// </summary>
    private Zeichengroesse Ausrichter(double y)
    {
        IReadOnlyList<Ausrichtertag> tage = d.Kompakttage ?? [];
        Titel("Ausrichter und Spielorte", y);
        y += 30;
        int spalten = d.Mannschaften.Select(m => tage.Count(t => Spielt(t, m.Name))).DefaultIfEmpty(0).Max();
        double rechts = Links + (spalten * (Kartenbreite + Kartenabstand)) + 12;
        double breite = rechts + 100;
        for (int j = 0; j < spalten; j++)
        {
            Text(string.Create(CultureInfo.InvariantCulture, $"{j + 1}. Spieltag"), Links + (j * (Kartenbreite + Kartenabstand)), y, Normal, Beschriftung);
        }

        Text("ausgerichtet", rechts, y, Normal, Beschriftung);
        y += 24;

        // Erst alle Karten verorten: Ein Hinweis umrahmt die Karten der anderen Mannschaften desselben Tags.
        var karten = new Dictionary<(DateOnly Tag, string Name), (double X, double Y)>();
        for (int i = 0; i < d.Mannschaften.Count; i++)
        {
            string name = d.Mannschaften[i].Name;
            double oben = y + (i * Zeilenabstand) + ((Zeilenabstand - Kartenhoehe) / 2);
            List<Ausrichtertag> eigene = tage.Where(t => Spielt(t, name)).ToList();
            for (int j = 0; j < eigene.Count; j++)
            {
                karten[(eigene[j].Tag, name)] = (Links + (j * (Kartenbreite + Kartenabstand)), oben);
            }
        }

        for (int i = 0; i < d.Mannschaften.Count; i++)
        {
            string name = d.Mannschaften[i].Name;
            double oben = y + (i * Zeilenabstand);
            if (i % 2 == 1)
            {
                Rechteck(Band, 0, oben, breite, oben + Zeilenabstand);
            }

            double mitte = oben + (Zeilenabstand / 2);
            Text(name, 0, mitte - 8);
            List<Ausrichtertag> eigene = tage.Where(t => Spielt(t, name)).ToList();
            foreach (Ausrichtertag t in eigene)
            {
                (double kx, double ky) = karten[(t.Tag, name)];
                Karte(t, name, kx, ky);
                hinweise.Add(new Diagrammhinweis(
                    kx + (Kartenbreite / 2), ky + (Kartenhoehe / 2), Kartenhoehe / 2, Hinweistext(t), Kartenbreite, Kartenhoehe, Marken: Tagesrahmen(t, karten)));
            }

            int ausgerichtet = eigene.Count(t => t.Ausrichter == name);
            f?.Text(string.Create(CultureInfo.InvariantCulture, $"{ausgerichtet}×"), rechts, mitte - 8, Normal, Tinte, true);
        }

        y += (d.Mannschaften.Count * Zeilenabstand) + 16;
        double x = 0;
        f?.Vieleck(Rundrechteck.Punkte(x, y, x + 30, y + 18, 5), Heimspiel, null, 0, geschlossen: true);
        x = Legendentext("richtet aus (eigene Halle)", x + 36, y);
        f?.Vieleck(Rundrechteck.Punkte(x, y, x + 30, y + 18, 5), Weiss, Kartenrand, 1.5, geschlossen: true);
        x = Legendentext("spielt in der Halle des Ausrichters", x + 36, y);
        return new Zeichengroesse(Math.Max(breite, x), y + 26);
    }

    /// <summary>Eine Karte: Tag und Ausrichter bzw. die Halle, in der die Mannschaft spielt.</summary>
    private void Karte(Ausrichtertag t, string name, double x, double y)
    {
        bool richtetAus = t.Ausrichter == name;
        IReadOnlyList<(double X, double Y)> umriss = Rundrechteck.Punkte(x, y, x + Kartenbreite, y + Kartenhoehe, Kartenrundung);
        f?.Vieleck(umriss, richtetAus ? Heimspiel : Weiss, richtetAus ? null : Kartenrand, 1.5, geschlossen: true);

        double mitte = y + (Kartenhoehe / 2);
        Text(t.Tag.ToString("dd.MM.", Deutsch), x + 8, mitte - 7, Klein + 1, richtetAus ? Blasstext : Beschriftung);
        string ort = richtetAus ? "Ausrichter" : "in " + (t.Ausrichter ?? "Halle offen");
        f?.Text(Kuerzen(ort, Kartenbreite - 52 - 8, Normal), x + 52, mitte - 8, Normal, richtetAus ? Weiss : Tinte, richtetAus);
    }

    private void Rahmen(double links, double oben, double rechts, double unten, Farbe farbe, double staerke) =>
        f?.Vieleck([(links, oben), (rechts, oben), (rechts, unten), (links, unten)], null, farbe, staerke, geschlossen: true);
}
