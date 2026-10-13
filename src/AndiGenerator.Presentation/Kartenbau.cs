// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;

namespace AndiGenerator.Presentation;

/// <summary>
/// Berechnet die Karte der Spielorte (nicht im Original): flächentreue Näherung um die mittlere Breite, Ausschnitt so
/// gezoomt, dass alle Spielorte mit Rand zu sehen sind; Beschriftungen ohne Überdeckung, nur wenige Städte.
/// </summary>
public static class Kartenbau
{
    /// <summary>Breite des Bilds in Bildpunkten.</summary>
    public const double Breite = 600;

    private const double Gradmass = 100;
    private const double Schrifthoehe = 15;
    private const int HoechstensStaedte = 10;

    /// <summary>Baut das Kartenbild.</summary>
    /// <param name="orte">Spielorte (nur solche mit bekannter Lage).</param>
    /// <param name="fahrten">Fahrten von Gast zu Ausrichter (Mannschaftsnamen), mit Kennzeichen für Einzelbegegnungen.</param>
    /// <param name="laender">Umrisse der Bundesländer.</param>
    /// <param name="staedte">Städte, die wichtigsten zuerst.</param>
    /// <returns>Das Bild; ohne Spielorte ein leeres.</returns>
    public static Kartenbild Bauen(
        IReadOnlyList<Kartenort> orte,
        IReadOnlyList<(string Von, string Nach, bool Kraeftig, bool Einzel)> fahrten,
        IReadOnlyList<Landesumriss> laender,
        IReadOnlyList<Stadt> staedte)
    {
        ArgumentNullException.ThrowIfNull(orte);
        ArgumentNullException.ThrowIfNull(fahrten);
        ArgumentNullException.ThrowIfNull(laender);
        ArgumentNullException.ThrowIfNull(staedte);
        if (orte.Count == 0)
        {
            return Kartenbild.Leer;
        }

        var projektion = new Projektion(orte.Select(o => o.Lage).ToList());
        var flaechen = new List<IReadOnlyList<Kartenpunkt>>();
        var landnamen = new List<Kartenbeschriftung>();
        foreach (Landesumriss land in laender)
        {
            flaechen.AddRange(land.Ringe.Select(r => (IReadOnlyList<Kartenpunkt>)r.Select(projektion.Bild).ToList()).Where(projektion.Sichtbar));
            if (land.Ringe.Count > 0)
            {
                IReadOnlyList<Koordinate> groesster = land.Ringe.MaxBy(r => r.Count)!;
                Kartenpunkt mitte = projektion.Bild(new Koordinate(groesster.Average(k => k.Breite), groesster.Average(k => k.Laenge)));
                if (mitte.X > 60 && mitte.X < Breite - 60 && mitte.Y > 20 && mitte.Y < projektion.Hoehe - 20)
                {
                    landnamen.Add(new Kartenbeschriftung(land.Name.Replace("Freie Hansestadt ", string.Empty, StringComparison.Ordinal), mitte, mitte));
                }
            }
        }

        var belegt = new List<(double L, double O, double R, double U)>();
        bool Frei((double L, double O, double R, double U) b) => !belegt.Exists(c => b.L < c.R && b.R > c.L && b.O < c.U && b.U > c.O);
        var hallen = new List<Kartenhalle>();
        var punkte = orte.ToDictionary(o => o.Name, o => projektion.Bild(o.Lage), StringComparer.Ordinal);
        belegt.AddRange(punkte.Values.Select(p => (p.X - 7, p.Y - 7, p.X + 7, p.Y + 7)));
        foreach (Kartenort ort in orte)
        {
            Kartenpunkt p = punkte[ort.Name];
            double w = (ort.Kurzname.Length * 7) + 4;
            Kartenpunkt[] lagen = [new(p.X + 9, p.Y - 16), new(p.X + 9, p.Y + 4), new(p.X - 9 - w, p.Y - 16), new(p.X - 9 - w, p.Y + 4), new(p.X - (w / 2), p.Y + 9), new(p.X - (w / 2), p.Y - 28)];
            Kartenpunkt schrift = Array.Find(lagen, l => Frei((l.X, l.Y, l.X + w, l.Y + Schrifthoehe)));
            if (schrift == default)
            {
                schrift = lagen[0];
            }

            belegt.Add((schrift.X, schrift.Y, schrift.X + w, schrift.Y + Schrifthoehe));
            hallen.Add(new Kartenhalle(ort, p, schrift));
        }

        var stadtnamen = new List<Kartenbeschriftung>();
        foreach (Stadt stadt in staedte)
        {
            Kartenpunkt p = projektion.Bild(stadt.Lage);
            double w = (stadt.Name.Length * 6.3) + 10;
            (double L, double O, double R, double U) box = (p.X - 4, p.Y - 9, p.X + w, p.Y + 6);
            if (p.X < 20 || p.X + w > Breite - 10 || p.Y < 15 || p.Y > projektion.Hoehe - 28 || !Frei(box))
            {
                continue;
            }

            belegt.Add((box.L - 25, box.O - 12, box.R + 25, box.U + 12));
            stadtnamen.Add(new Kartenbeschriftung(stadt.Name, p, new Kartenpunkt(p.X + 6, p.Y - 8)));
            if (stadtnamen.Count >= HoechstensStaedte)
            {
                break;
            }
        }

        List<Kartenfahrt> linien = Nebeneinander(fahrten.Where(f => punkte.ContainsKey(f.Von) && punkte.ContainsKey(f.Nach)).ToList(), punkte);
        double kmJePunkt = projektion.Massstab * Gradmass / 111.2;
        int massstab = new[] { 200, 100, 50, 20, 10, 5 }.FirstOrDefault(km => km * kmJePunkt <= 140, 5);
        return new Kartenbild(Breite, projektion.Hoehe, flaechen, landnamen, stadtnamen, hallen, linien, massstab, massstab * kmJePunkt);
    }

    /// <summary>
    /// Fahrten zwischen denselben beiden Hallen (in beide Richtungen) bekommen einen seitlichen Versatz, damit ihre Linien
    /// nebeneinander liegen statt übereinander.
    /// </summary>
    private static List<Kartenfahrt> Nebeneinander(List<(string Von, string Nach, bool Kraeftig, bool Einzel)> fahrten, Dictionary<string, Kartenpunkt> punkte)
    {
        var ergebnis = new List<Kartenfahrt>();
        foreach (IGrouping<(string, string), (string Von, string Nach, bool Kraeftig, bool Einzel)> gruppe in fahrten
            .GroupBy(f => string.CompareOrdinal(f.Von, f.Nach) <= 0 ? (f.Von, f.Nach) : (f.Nach, f.Von)))
        {
            List<(string Von, string Nach, bool Kraeftig, bool Einzel)> liste = gruppe.ToList();
            for (int i = 0; i < liste.Count; i++)
            {
                (string von, string nach, bool kraeftig, bool einzel) = liste[i];

                // Versatz gilt in Richtung der Gruppe (erster Name zum zweiten); in Gegenrichtung mit umgekehrtem Vorzeichen.
                double versatz = i - ((liste.Count - 1) / 2.0);
                if (von != gruppe.Key.Item1)
                {
                    versatz = -versatz;
                }

                ergebnis.Add(new Kartenfahrt(punkte[von], punkte[nach], kraeftig, einzel) { Versatz = versatz, Mehrfach = liste.Count > 1 });
            }
        }

        return ergebnis;
    }

    /// <summary>Gleichabstandsprojektion um die mittlere Breite der Spielorte, auf den Ausschnitt skaliert.</summary>
    private sealed class Projektion
    {
        private readonly double faktorLaenge;
        private readonly double links;
        private readonly double oben;

        public Projektion(List<Koordinate> orte)
        {
            double mitteBreite = orte.Average(o => o.Breite);
            faktorLaenge = Math.Cos(mitteBreite * Math.PI / 180) * Gradmass;
            double x0 = orte.Min(o => o.Laenge * faktorLaenge);
            double x1 = orte.Max(o => o.Laenge * faktorLaenge);
            double y0 = orte.Min(o => -o.Breite * Gradmass);
            double y1 = orte.Max(o => -o.Breite * Gradmass);
            double randX = Math.Max(((x1 - x0) * 0.22) + 12, (120 - (x1 - x0)) / 2);
            double randY = Math.Max(((y1 - y0) * 0.22) + 12, (80 - (y1 - y0)) / 2);
            (x0, x1, y0, y1) = (x0 - randX, x1 + randX, y0 - randY, y1 + randY);

            // Seitenverhältnis zwischen 0,55 und 1,0: die kürzere Seite wird verlängert.
            double w = x1 - x0;
            double h = y1 - y0;
            if (h < 0.55 * w)
            {
                (y0, y1) = (y0 - (((0.55 * w) - h) / 2), y1 + (((0.55 * w) - h) / 2));
            }
            else if (h > w)
            {
                (x0, x1) = (x0 - ((h - w) / 2), x1 + ((h - w) / 2));
            }

            links = x0;
            oben = y0;
            Massstab = Breite / (x1 - x0);
            Hoehe = (y1 - y0) * Massstab;
        }

        public double Massstab { get; }

        public double Hoehe { get; }

        public Kartenpunkt Bild(Koordinate k) =>
            new(((k.Laenge * faktorLaenge) - links) * Massstab, ((-k.Breite * Gradmass) - oben) * Massstab);

        public bool Sichtbar(IReadOnlyList<Kartenpunkt> ring) =>
            ring.Any(p => p.X > -Breite && p.X < 2 * Breite && p.Y > -Hoehe && p.Y < 2 * Hoehe);
    }
}
