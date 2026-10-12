// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Kompaktplaner: vollständige, gültige Pläne aus 3er- und 4er-Spieltagen, Festhalten, Fahrtwünsche, Meldungen.</summary>
public class KompaktplanerTests
{
    private static readonly DateOnly Erster = new(2026, 9, 6);

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Varianten_sind_gueltige_Plaene(int anzahl)
    {
        Kompaktaufgabe aufgabe = Aufgabe(anzahl, [], []);

        Kompaktergebnis ergebnis = Planen(aufgabe);

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.Equal("Ausgewogen", ergebnis.Varianten[0].Name.Split(" · ")[0]);
        foreach (Kompaktvariante v in ergebnis.Varianten)
        {
            Turnierpruefungsergebnis pruefung = Turnierpruefung.Pruefen(Turniere(v), aufgabe.Mannschaften, aufgabe.Meldungen);
            Assert.Empty(pruefung.Befunde);
            Assert.Equal(0, v.Kennzahlen.Verstoesse);
            Assert.Equal(0, v.Kennzahlen.OhneHeimspieltag);
            int einzel = anzahl % 2 == 0 ? 1 : 0;
            Assert.All(aufgabe.Mannschaften, m => Assert.Equal(einzel, v.Turniere.Count(t => t.Form == Turnierform.Einzel && t.Mannschaften.Contains(m))));
        }
    }

    [Fact]
    public void Bei_neun_Mannschaften_nur_4er_Spieltage()
    {
        Kompaktergebnis ergebnis = Planen(Aufgabe(9, [], []));

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.All(ergebnis.Varianten, v => Assert.Equal((9, 0, 0), (v.Kennzahlen.Vierer, v.Kennzahlen.Dreier, v.Kennzahlen.Einzel)));
    }

    [Fact]
    public void Kuerzeste_Fahrten_sind_nicht_laenger_als_die_anderen_Varianten()
    {
        Kompaktergebnis ergebnis = Planen(Aufgabe(9, [], []));

        Kompaktvariante kuerzeste = ergebnis.Varianten.Single(v => v.Name.Contains("Kürzeste Fahrten", StringComparison.Ordinal));
        Assert.All(ergebnis.Varianten.Where(v => v.Kennzahlen.Dreier == 0), v => Assert.True(kuerzeste.Kennzahlen.Km <= v.Kennzahlen.Km + 1e-6));
    }

    [Fact]
    public void Festgehaltener_Spieltag_steht_in_jeder_Variante()
    {
        Kompaktaufgabe ohne = Aufgabe(9, [], []);
        Planturnier fest = Planen(ohne).Varianten[0].Turniere[^1];

        Kompaktergebnis ergebnis = Planen(ohne with { Festgehalten = [fest] });

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.All(ergebnis.Varianten, v => Assert.Contains(v.Turniere, t => t.Tag == fest.Tag && t.Mannschaften.SequenceEqual(fest.Mannschaften)));
    }

    [Fact]
    public void Fahrtwunsch_findet_in_der_Halle_des_Gastgebers_statt()
    {
        Kompaktergebnis ergebnis = Planen(Aufgabe(9, [], [new Fahrtwunsch("M1", "M9")]));

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.All(ergebnis.Varianten, v =>
        {
            Planturnier t = v.Turniere.Single(x => x.Begegnungen().Any(b => (b.Heim, b.Gast) is ("M1", "M9") or ("M9", "M1")));
            Assert.Equal("M9", t.Ausrichter);
        });
    }

    [Fact]
    public void Gesperrte_Sonntage_bleiben_frei()
    {
        DateOnly gesperrt = Erster.AddDays(7);
        Kompaktaufgabe aufgabe = Aufgabe(9, [("M3", gesperrt)], []);

        Kompaktergebnis ergebnis = Planen(aufgabe);

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.All(ergebnis.Varianten, v => Assert.DoesNotContain(v.Turniere, t => t.Tag == gesperrt && t.Mannschaften.Contains("M3")));
    }

    [Fact]
    public void Bei_sechs_Mannschaften_reichen_die_Spieltage_nicht_fuer_jeden_Heimspieltag()
    {
        Kompaktergebnis ergebnis = Planen(Aufgabe(6, [], []));

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.Contains(ergebnis.Hinweise, h => h.Contains("keinen Spieltag aus", StringComparison.Ordinal));
    }

    [Fact]
    public void Begegnungen_des_Vierers_in_zwei_Runden()
    {
        var vierer = new Planturnier(Erster, Turnierform.Vierer, ["A", "B", "C", "D"]);

        Assert.Equal(new (int, string, string)[] { (0, "A", "C"), (0, "B", "D"), (1, "A", "D"), (1, "B", "C") }, vierer.Begegnungen());
    }

    private static Kompaktergebnis Planen(Kompaktaufgabe aufgabe) =>
        Kompaktplaner.Planen(aufgabe, new Kompaktplanoptionen(TimeSpan.FromMinutes(1), 1, 17, 400, TimeSpan.FromSeconds(5)), CancellationToken.None, null);

    /// <summary>Mannschaften M1 … Mn auf einem Kreis (Entfernung = Abstand der Punkte in km), zehn Sonntage, alle Heim und auswärts.</summary>
    private static Kompaktaufgabe Aufgabe(int anzahl, IReadOnlyList<(string Mannschaft, DateOnly Tag)> sperren, IReadOnlyList<Fahrtwunsch> wuensche)
    {
        List<string> namen = Enumerable.Range(1, anzahl).Select(i => "M" + i).ToList();
        List<DateOnly> tage = Enumerable.Range(0, 10).Select(i => Erster.AddDays(7 * i)).ToList();
        List<Turniermeldung> meldungen = namen.Select(n => new Turniermeldung(
            n,
            tage.ToDictionary(t => t, t => sperren.Contains((n, t)) ? Turnierbereitschaft.None : Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts),
            "Halle " + n,
            true,
            string.Empty)).ToList();
        double Entfernung(string a, string b)
        {
            double wa = 2 * Math.PI * int.Parse(a[1..], System.Globalization.CultureInfo.InvariantCulture) / anzahl;
            double wb = 2 * Math.PI * int.Parse(b[1..], System.Globalization.CultureInfo.InvariantCulture) / anzahl;
            return 100 * Math.Sqrt(Math.Pow(Math.Cos(wa) - Math.Cos(wb), 2) + Math.Pow(Math.Sin(wa) - Math.Sin(wb), 2));
        }

        return new Kompaktaufgabe(namen, tage, meldungen, Entfernung, [], wuensche);
    }

    private static List<Turnier> Turniere(Kompaktvariante v) => v.Turniere
        .Select(t => new Turnier(
            t.Tag,
            t.Form,
            t.Mannschaften,
            t.Begegnungen().Select(b => new Spiel(t.Tag.ToDateTime(new TimeOnly(11 + (3 * b.Runde), 0)), b.Heim, b.Gast, string.Empty)).ToList(),
            [t.Ausrichter]))
        .ToList();
}
