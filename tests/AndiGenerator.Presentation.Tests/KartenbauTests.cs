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
            new("ESV Engers", "Engers", new Koordinate(50.437, 7.545), false, false, string.Empty),
            new("TTC Zugbrücke Grenzau", "Grenzau", new Koordinate(50.435, 7.669), true, false, string.Empty),
            new("SV Trier-Olewig", "Trier-Olewig", new Koordinate(49.750, 6.633), false, false, string.Empty),
            new("TV Dienheim", "Dienheim", new Koordinate(49.837, 8.350), false, false, string.Empty),
        ];

        Kartenbild bild = Kartenbau.Bauen(orte, [("ESV Engers", "TTC Zugbrücke Grenzau", true, false), ("SV Trier-Olewig", "Unbekannt", true, true)], Kartendaten.Laender, Kartendaten.Staedte);

        Assert.Equal(600, bild.Breite);
        Assert.InRange(bild.Hoehe / bild.Breite, 0.55, 1.0);
        Assert.All(bild.Hallen, h => Assert.True(h.Punkt.X is > 0 and < 600 && h.Punkt.Y > 0 && h.Punkt.Y < bild.Hoehe));
        Assert.Single(bild.Fahrten);
        Assert.InRange(bild.Staedte.Count, 1, 10);
        Assert.NotEmpty(bild.Flaechen);
        Assert.Contains(bild.Laender, l => l.Text == "Rheinland-Pfalz");
        var namen = bild.Hallen.Select(h => (h.Schrift.X, h.Schrift.Y, R: h.Schrift.X + (h.Ort.Kurzname.Length * 7) + 4, U: h.Schrift.Y + 15)).ToList();
        for (int i = 0; i < namen.Count; i++)
        {
            for (int j = i + 1; j < namen.Count; j++)
            {
                bool ueberlappt = namen[i].X < namen[j].R && namen[i].R > namen[j].X && namen[i].Y < namen[j].U && namen[i].U > namen[j].Y;
                Assert.False(ueberlappt, $"{bild.Hallen[i].Ort.Kurzname} und {bild.Hallen[j].Ort.Kurzname} überdecken sich.");
            }
        }
    }

    [Fact]
    public void Ohne_Spielorte_ist_die_Karte_leer() =>
        Assert.Same(Kartenbild.Leer, Kartenbau.Bauen([], [], Kartendaten.Laender, Kartendaten.Staedte));
}
