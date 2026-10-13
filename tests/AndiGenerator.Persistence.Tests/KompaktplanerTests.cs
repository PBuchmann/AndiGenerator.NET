// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Kompaktplaner: vollständige, gültige Pläne aus 3er- und 4er-Spieltagen, Meldungen. Läuft allein (zeitbegrenzte Suchen
/// würden sich sonst die Kerne mit anderen Tests teilen und unter Last schlechter abschneiden).
/// </summary>
[Collection(MessungenSammlung.Name)]
public class KompaktplanerTests
{
    private static readonly DateOnly Erster = new(2026, 9, 6);

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Varianten_sind_gueltige_Plaene(int anzahl)
    {
        Kompaktaufgabe aufgabe = Aufgabe(anzahl, []);

        Kompaktergebnis ergebnis = Planen(aufgabe);

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.Contains(ergebnis.Varianten[0].Name.Split(" · ")[0], new[] { "Ausgewogen", "Bestes Gesamtpaket" });
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
        Kompaktergebnis ergebnis = Planen(Aufgabe(9, []));

        Assert.NotEmpty(ergebnis.Varianten);
        List<Kompaktvariante> vierer = ergebnis.Varianten.Where(v => !v.Name.StartsWith(Kompaktplaner.MitDreiern, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(vierer);
        Assert.All(vierer, v => Assert.Equal((9, 0, 0), (v.Kennzahlen.Vierer, v.Kennzahlen.Dreier, v.Kennzahlen.Einzel)));

        // Varianten mit 3er-Spieltagen nur, wenn sie ein Ziel besser treffen als jeder Plan nur aus 4er-Spieltagen.
        foreach (Kompaktvariante d in ergebnis.Varianten.Except(vierer))
        {
            Kompaktkennzahlen k = d.Kennzahlen;
            Assert.True(k.Dreier > 0);
            bool besser = k.Km < vierer.Min(v => v.Kennzahlen.Km) || k.Gemeinsam > vierer.Max(v => v.Kennzahlen.Gemeinsam)
                || k.KmSpanne < vierer.Min(v => v.Kennzahlen.KmSpanne) || k.Abstand < vierer.Min(v => v.Kennzahlen.Abstand);
            Assert.True(besser, d.Name);
        }
    }

    [Fact]
    public void Spitzen_und_Abstiegsspieltag_so_spaet_wie_moeglich()
    {
        Kompaktaufgabe aufgabe = Aufgabe(9, []) with { Spitze = ["M1", "M2", "M3", "M4"], Keller = ["M9", "M8", "M7", "M6"] };

        Kompaktergebnis ergebnis = Planen(aufgabe);

        // Eigene Variante – oder das beste Gesamtpaket hat beide Spieltage schon, dann steht es dort unter „zugleich“.
        Kompaktvariante? eigene = ergebnis.Varianten.SingleOrDefault(x => x.Name == Kompaktplaner.SpitzeUndKeller);
        Kompaktvariante v = eigene ?? ergebnis.Varianten[0];
        Assert.True(eigene is not null || v.Zugleich.Contains(Kompaktplaner.SpitzeUndKeller));
        Planturnier spitze = Assert.Single(v.Turniere, t => t.Mannschaften.Order().SequenceEqual(["M1", "M2", "M3", "M4"]));
        Planturnier keller = Assert.Single(v.Turniere, t => t.Mannschaften.Order().SequenceEqual(["M6", "M7", "M8", "M9"]));
        if (eigene is not null)
        {
            DateOnly letzter = aufgabe.Tage[^1];
            Assert.Equal((letzter, letzter), (spitze.Tag, keller.Tag));
        }

        Assert.Contains(spitze.Begegnungen(), b => (b.Heim, b.Gast) is ("M1", "M2") or ("M2", "M1"));
        Assert.Contains(keller.Begegnungen(), b => (b.Heim, b.Gast) is ("M9", "M8") or ("M8", "M9"));
        Assert.Contains(ergebnis.Hinweise, h => h.StartsWith("Spitzenspieltag am", StringComparison.Ordinal));

        // Ganz vorn steht das beste Gesamtpaket aus allen Zielen.
        Assert.Equal("Bestes Gesamtpaket", ergebnis.Varianten[0].Name);
        Assert.Contains(ergebnis.Hinweise, h => h.StartsWith("Bestes Gesamtpaket:", StringComparison.Ordinal));
    }

    [Fact]
    public void Was_das_Gesamtpaket_schon_erfuellt_entfaellt_als_eigene_Variante()
    {
        Kompaktvariante gesamt = Variante("Bestes Gesamtpaket", km: 3100, abstand: 2, gemeinsam: 8, spanne: 300);
        List<Kompaktvariante> varianten =
        [
            gesamt,
            Variante("Kürzeste Fahrten · Gemeinsamer Spieltag", km: 3000, abstand: 3, gemeinsam: 8, spanne: 400),
            Variante("Gleichmäßiger Stand", km: 3200, abstand: 1, gemeinsam: 4, spanne: 350),
            Variante("Gleich weite Fahrten", km: 3300, abstand: 2, gemeinsam: 4, spanne: 300),
            Variante(Kompaktplaner.SpitzeUndKeller, km: 3400, abstand: 2, gemeinsam: 4, spanne: 500),
        ];

        List<Kompaktvariante> ergebnis = Kompaktplaner.Zusammenfassen(varianten, gesamtMitGruppen: true);

        Assert.Equal(new[] { "Bestes Gesamtpaket", "Kürzeste Fahrten", "Gleichmäßiger Stand" }, ergebnis.Select(v => v.Name).ToArray());
        Assert.Equal(new[] { "Gemeinsamer Spieltag", "Gleich weite Fahrten", Kompaktplaner.SpitzeUndKeller }, ergebnis[0].Zugleich.ToArray());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    public void Nachsuche_findet_einen_Plan_ganz_ohne_Verstoesse(int anzahl)
    {
        var modell = new Kompaktmodell(Aufgabe(anzahl, []));
        var baum = new Meldungsbaum(modell);
        using var steuerung = new Kompaktsteuerung();

        IReadOnlyList<Kompaktkandidat>? turniere = baum.Suchen(0, modell.MindestensOhneAusrichtung, false, TimeSpan.FromSeconds(20), steuerung);

        Assert.NotNull(turniere);
        var loesung = new Kompaktloesung(modell, turniere);
        Assert.Equal(0, loesung.Verstoesse);
        Assert.Equal(modell.Paare, turniere.Sum(k => k.Paare.Length));
        Assert.True(loesung.OhneHeimspieltag <= modell.MindestensOhneAusrichtung);
    }

    [Fact]
    public void Unter_der_Woche_nur_Einzelbegegnungen_bis_60_km()
    {
        var mittwoch = new DateOnly(2026, 9, 9);
        var samstag = new DateOnly(2026, 9, 12);
        List<string> namen = ["A", "B", "C", "D"];
        var bereit = new Dictionary<DateOnly, Turnierbereitschaft>
        {
            [mittwoch] = Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts,
            [samstag] = Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts,
        };
        List<Turniermeldung> meldungen = namen.Select(n => new Turniermeldung(n, bereit, "Halle", true, string.Empty)).ToList();
        static double Entfernung(string a, string b) => (a, b) is ("A", "D") or ("D", "A") ? 80 : 20;

        var modell = new Kompaktmodell(new Kompaktaufgabe(namen, [mittwoch, samstag], meldungen, Entfernung));
        int mi = Array.IndexOf(modell.Tage, mittwoch);
        int sa = Array.IndexOf(modell.Tage, samstag);

        Assert.Equal(0, modell.Verstoesse(Turnierform.Einzel, [0, 1], mi));
        Assert.True(modell.Verstoesse(Turnierform.Einzel, [0, 3], mi) > Kompaktmodell.MaxVerstoesseJeSpieltag);
        Assert.True(modell.Verstoesse(Turnierform.Dreier, [0, 1, 2], mi) > Kompaktmodell.MaxVerstoesseJeSpieltag);
        Assert.Equal(0, modell.Verstoesse(Turnierform.Vierer, [0, 1, 2, 3], sa));
    }

    [Fact]
    public void Kuerzeste_Fahrten_sind_nicht_laenger_als_die_anderen_Varianten()
    {
        Kompaktergebnis ergebnis = Planen(Aufgabe(9, []));

        Kompaktvariante kuerzeste = ergebnis.Varianten.Single(v => v.Name.Contains("Kürzeste Fahrten", StringComparison.Ordinal));
        Assert.All(ergebnis.Varianten.Where(v => v.Kennzahlen.Dreier == 0), v => Assert.True(kuerzeste.Kennzahlen.Km <= v.Kennzahlen.Km + 1e-6));
    }

    [Fact]
    public void HiGHS_ist_verfuegbar() => Assert.True(Highsbruecke.Verfuegbar);

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Gezielte_Konstruktion_liefert_gueltige_Plaene(int anzahl)
    {
        Kompaktaufgabe aufgabe = Aufgabe(anzahl, [("M1", Erster), ("M2", Erster.AddDays(7))]);

        Kompaktergebnis ergebnis = Kompaktplaner.Planen(
            aufgabe,
            new Kompaktplanoptionen(TimeSpan.FromSeconds(1), 1, 17, 20, TimeSpan.FromSeconds(20)) { Loeser = new Highsbruecke() },
            CancellationToken.None,
            null);

        Assert.NotEmpty(ergebnis.Varianten);
        Assert.Contains(ergebnis.Hinweise, h => h.StartsWith("Plan mit den wenigsten Punkten", StringComparison.Ordinal));
        foreach (Kompaktvariante v in ergebnis.Varianten)
        {
            Turnierpruefungsergebnis pruefung = Turnierpruefung.Pruefen(Turniere(v), aufgabe.Mannschaften, aufgabe.Meldungen);
            Assert.Empty(pruefung.Befunde);
            Assert.Equal(0, v.Kennzahlen.Verstoesse);
            Assert.Equal(0, v.Kennzahlen.OhneHeimspieltag);
        }
    }

    [Fact]
    public void Kuerzester_4er_Plan_wie_die_vollstaendige_Suche()
    {
        var modell = new Kompaktmodell(Aufgabe(9, []));
        var baum = new Viererbaum(modell, [], "Nur 4er-Spieltage");
        using var steuerung = new Kompaktsteuerung();
        baum.Suchen(1, TimeSpan.FromSeconds(30), steuerung, null);

        (Kompaktloesung? loesung, bool optimal) = new Kompaktoptimierung(modell, new Highsbruecke()).Suchen(new Kompaktauftrag(null, true, false, false, false), null, 2, () => false);

        Assert.True(baum.Vollstaendig);
        Assert.NotNull(baum.Beste);
        Assert.NotNull(loesung);
        Assert.True(optimal);
        Assert.Equal((9, 0), (loesung.Vierer, loesung.Dreier));
        Assert.Equal(baum.Beste.Sum(k => k.Km), loesung.Km, 3);
    }

    private static Kompaktergebnis Planen(Kompaktaufgabe aufgabe) =>
        Kompaktplaner.Planen(aufgabe, new Kompaktplanoptionen(TimeSpan.FromMinutes(1), 1, 17, 400, TimeSpan.FromSeconds(5)), CancellationToken.None, null);

    /// <summary>Mannschaften M1 … Mn auf einem Kreis (Entfernung = Abstand der Punkte in km), zehn Sonntage, alle Heim und auswärts.</summary>
    private static Kompaktaufgabe Aufgabe(int anzahl, IReadOnlyList<(string Mannschaft, DateOnly Tag)> sperren)
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

        return new Kompaktaufgabe(namen, tage, meldungen, Entfernung);
    }

    private static List<Turnier> Turniere(Kompaktvariante v) => v.Turniere
        .Select(t => new Turnier(
            t.Tag,
            t.Form,
            t.Mannschaften,
            t.Begegnungen().Select(b => new Spiel(t.Tag.ToDateTime(new TimeOnly(11 + (3 * b.Runde), 0)), b.Heim, b.Gast, string.Empty)).ToList(),
            [t.Ausrichter]))
        .ToList();

    private static Kompaktvariante Variante(string name, double km, int abstand, int gemeinsam, double spanne) =>
        new(name, [], new Kompaktkennzahlen(km, abstand, gemeinsam, 0, 0, 0, 9, 0, 0, spanne));
}
