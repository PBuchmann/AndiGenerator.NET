// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation.Tests;

public sealed class WechselTests
{
    private static readonly DateTime Start = new(2026, 9, 6, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void SerienFolgenDenTerminen()
    {
        // In der Liste steht das dritte Spiel vorn; es zählt nach seinem Termin. Spiele ohne Termin fehlen.
        Diagrammmannschaft m = Mannschaft("A", "HAAAHH", vorn: 2);

        Assert.Equal([(true, 0, 1), (false, 1, 3), (true, 4, 2)], Diagrammzeichner.Serien(m));
    }

    [Fact]
    public void DiagrammZeigtSerienUndBrueche()
    {
        var daten = new Diagrammdaten(true, Start, Start.AddDays(70), [Mannschaft("A", "HAAAHH"), Mannschaft("B", "AHAHAH")], true, [], false, 6);
        var flaeche = new Messflaeche();

        var zeichner = new Diagrammzeichner(daten, flaeche);
        Zeichengroesse groesse = zeichner.Zeichnen(Diagrammteil.WechselHeimAuswaerts, 0);

        // Brüche: A dreimal (AA, AA, HH), B nie.
        Assert.Contains("3", flaeche.Texte);
        Assert.Contains("0", flaeche.Texte);
        Assert.Contains("Serie ab drei", flaeche.Texte);

        // Je Spiel eine Kachel mit Hinweis; die Serie nennt der Hinweis, nicht die Kachel.
        Assert.Equal(12, zeichner.Hinweise.Count);
        Assert.Contains("3 Auswärtsspiele hintereinander", zeichner.Hinweise[2].Text, StringComparison.Ordinal);
        Assert.StartsWith("1. Spiel, ", zeichner.Hinweise[0].Text, StringComparison.Ordinal);

        // Die Lücke vor der Rückrunde: Das vierte Spiel steht weiter vom dritten weg als das dritte vom zweiten.
        Assert.True(zeichner.Hinweise[3].X - zeichner.Hinweise[2].X > zeichner.Hinweise[2].X - zeichner.Hinweise[1].X);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.WechselHeimAuswaerts, 0));
    }

    [Fact]
    public void KompaktspieltageZeigenAusrichterUndSpielorte()
    {
        // Ein Dreier in der Halle von A (B hat gegen C Heimrecht laut Plan, das zählt hier nicht).
        List<Spiel> spiele =
        [
            new(Start, "A", "B", "Halle A"),
            new(Start.AddHours(2), "B", "C", string.Empty),
            new(Start.AddHours(4), "A", "C", "Halle A"),
        ];
        IReadOnlyList<Ausrichtertag> tage = Kompaktplanung.Ausrichtertage(spiele);
        Assert.Equal("A", Assert.Single(tage).Ausrichter);

        List<Diagrammmannschaft> mannschaften = new[] { "A", "B", "C" }
            .Select(n => new Diagrammmannschaft(n, spiele.Where(s => s.Heim == n || s.Gast == n).Select(s => new Diagrammspiel(s.Zeitpunkt, s.Heim == n, false, 1, 0)).ToList(), []))
            .ToList();
        var daten = new Diagrammdaten(true, Start, Start, mannschaften, false, [], false, 2, tage);
        Assert.Equal([Diagrammteil.Spieltage, Diagrammteil.Ausrichter, Diagrammteil.Spielverteilung, Diagrammteil.SpieleProWoche], Diagrammzeichner.Teile(daten));

        var flaeche = new Messflaeche();
        var zeichner = new Diagrammzeichner(daten, flaeche);
        Zeichengroesse groesse = zeichner.Zeichnen(Diagrammteil.Ausrichter, 0);

        Assert.Contains("Ausrichter", flaeche.Texte);
        Assert.Contains("in A", flaeche.Texte);
        Assert.Contains("1×", flaeche.Texte);
        Assert.DoesNotContain(flaeche.Texte, x => x is "H" or "G");
        Assert.Equal(3, zeichner.Hinweise.Count);
        Diagrammhinweis a = zeichner.Hinweise[0];
        Assert.Contains("Ausrichter: A", a.Text, StringComparison.Ordinal);

        // Die ganze Karte löst den Hinweis aus; umrahmt werden die Karten aller drei Mannschaften des Spieltags.
        Assert.True(a.Trifft(a.Links + 2, a.Oben + 2));
        Assert.True(a.Trifft(a.Rechts - 2, a.Unten - 2));
        Assert.False(a.Trifft(a.Rechts + 2, a.Y));
        Assert.All(zeichner.Hinweise, h => Assert.Equal(3, h.Marken!.Count));
        Assert.Contains(a.Marken!, m => Math.Abs(m.X - a.X) < 0.01 && Math.Abs(m.Y - a.Y) < 0.01 && m.Breite > a.Breite);

        // Der Kasten des Hinweises verdeckt weder die Karte noch die Rahmen.
        var kasten = new Messflaeche();
        (double x, _) = Hinweiszeichner.Zeichnen(kasten, a, 2000, 1000);
        Assert.True(x > a.Marken!.Max(m => m.X + (m.Breite / 2)));
        Assert.Contains("A – B", string.Join("|", kasten.Texte), StringComparison.Ordinal);
        Assert.Equal(groesse, new Diagrammzeichner(daten, null).Zeichnen(Diagrammteil.Ausrichter, 0));
    }

    private static Diagrammmannschaft Mannschaft(string name, string folge, int vorn = -1)
    {
        List<Diagrammspiel> spiele = folge.Select((c, i) => new Diagrammspiel(Start.AddDays(7 * i), c == 'H', false, i < folge.Length / 2 ? 1 : 2, 0)).ToList();
        if (vorn >= 0)
        {
            Diagrammspiel s = spiele[vorn];
            spiele.RemoveAt(vorn);
            spiele.Insert(0, s);
            spiele.Add(new Diagrammspiel(null, true, false, -1, 0));
        }

        return new Diagrammmannschaft(name, spiele, []);
    }
}
