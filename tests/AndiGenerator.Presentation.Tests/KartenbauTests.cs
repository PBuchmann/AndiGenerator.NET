// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Geo;
using AndiGenerator.Persistence.Geo;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Karte der Kompaktspieltage: Ausschnitt, Beschriftungen, Fahrten.</summary>
public sealed class KartenbauTests
{
    [Fact]
    public void Karte_zeigt_alle_Spielorte_ohne_ueberlappende_Namen()
    {
        Kartenort[] orte =
        [
            new("ESV Engers", "ESV Engers", new Koordinate(50.437, 7.545), false, false, string.Empty),
            new("TTC Zugbrücke Grenzau", "TTC Zugbrücke Grenzau", new Koordinate(50.435, 7.669), true, false, string.Empty),
            new("SV Trier-Olewig", "SV Trier-Olewig", new Koordinate(49.750, 6.633), false, false, string.Empty),
            new("TV Dienheim", "TV Dienheim", new Koordinate(49.837, 8.350), false, false, string.Empty),
        ];

        Kartenbild bild = Kartenbau.Bauen(orte, [("ESV Engers", "TTC Zugbrücke Grenzau", true, false), ("SV Trier-Olewig", "Unbekannt", true, true)], Kartendaten.Laender, Kartendaten.Staedte);

        Assert.Equal(600, bild.Breite);
        Assert.InRange(bild.Hoehe / bild.Breite, 0.55, 1.0);
        Assert.All(bild.Hallen, h => Assert.True(h.Punkt.X is > 0 and < 600 && h.Punkt.Y > 0 && h.Punkt.Y < bild.Hoehe));
        Assert.Single(bild.Fahrten);
        Assert.InRange(bild.Staedte.Count, 1, 10);
        Assert.NotEmpty(bild.Flaechen);
        Assert.Contains(bild.Laender, l => l.Text == "Rheinland-Pfalz");
        var namen = bild.Hallen.Select(h => (h.Schrift.X, h.Schrift.Y, R: h.Schrift.X + (h.Ort.Beschriftung.Length * 7) + 4, U: h.Schrift.Y + 15)).ToList();
        for (int i = 0; i < namen.Count; i++)
        {
            for (int j = i + 1; j < namen.Count; j++)
            {
                bool ueberlappt = namen[i].X < namen[j].R && namen[i].R > namen[j].X && namen[i].Y < namen[j].U && namen[i].U > namen[j].Y;
                Assert.False(ueberlappt, $"{bild.Hallen[i].Ort.Beschriftung} und {bild.Hallen[j].Ort.Beschriftung} überdecken sich.");
            }
        }
    }

    [Fact]
    public void Dichte_Stadtklasse_zeigt_jeden_vollen_Namen_ohne_Ueberdeckung()
    {
        // Hamburger 1. Landesliga 1 (Lage aus Postleitzahl): Ritterstraße und Richardstraße liegen auf demselben Punkt.
        (string Name, string Plz, string Ort)[] hallen =
        [
            ("TTC Neuenfelde", "21129", "Hamburg"), ("Oberalster VfW IV", "22339", "Hamburg"), ("Eimsbütteler TV", "20259", "Hamburg"),
            ("Wandsbeker Turnerbund 1861", "22041", "Hamburg"), ("TH Eilbeck III", "22089", "Hamburg"), ("GW Harburg II", "21077", "Hamburg"),
            ("SC Vorwärts/Wacker", "22113", "Hamburg"), ("Horner TV", "22111", "Hamburg"), ("TTSG Urania-Bramfeld II", "22305", "Hamburg"),
            ("TSV Glinde", "21509", "Glinde"), ("Zweite Halle in Eilbeck", "22089", "Hamburg"),
        ];
        Kartenort[] orte = hallen.Select(h => new Kartenort(h.Name, h.Name, Postleitzahlen.Lage(h.Plz, h.Ort)!.Value, false, false, string.Empty)).ToArray();
        Kartenbild bild = Kartenbau.Bauen(orte, [], Kartendaten.Laender, Kartendaten.Staedte);
        var f = new Messflaeche();

        new Kartenzeichner(f).Zeichnen(bild, bild.Breite, bild.Hoehe);

        // Der Ausschnitt ist auf die Stadt gezoomt: die Hallen nutzen einen großen Teil der Breite.
        double spanne = bild.Hallen.Max(h => h.Punkt.X) - bild.Hallen.Min(h => h.Punkt.X);
        Assert.True(spanne > 0.4 * bild.Breite, $"Hallen nur über {spanne:N0} von {bild.Breite:N0} Bildpunkten.");

        // Jeder volle Name steht an der Karte oder in der Legende (dann eine Nummer am Punkt).
        Assert.All(hallen, h => Assert.Contains(h.Name, f.Texte));

        // Namen und Nummern an den Punkten (12 pt, fett) überdecken sich nicht.
        var marken = f.Punkte.Where(p => p.Art != "Kreis").Zip(f.Texte)
            .Where(x => x.First.Art == "Text" && Math.Abs(x.First.Groesse - 12) < 1e-9)
            .Select(x => (L: x.First.X, O: x.First.Y, R: x.First.X + f.TextBreite(x.Second, 12, true), U: x.First.Y + 15, Text: x.Second))
            .ToList();
        Assert.Equal(hallen.Length, marken.Count);
        for (int i = 0; i < marken.Count; i++)
        {
            for (int j = i + 1; j < marken.Count; j++)
            {
                bool ueberlappt = marken[i].L < marken[j].R && marken[i].R > marken[j].L && marken[i].O < marken[j].U && marken[i].U > marken[j].O;
                Assert.False(ueberlappt, $"{marken[i].Text} und {marken[j].Text} überdecken sich.");
            }
        }
    }

    [Fact]
    public void Ohne_Spielorte_ist_die_Karte_leer() =>
        Assert.Same(Kartenbild.Leer, Kartenbau.Bauen([], [], Kartendaten.Laender, Kartendaten.Staedte));
}
