// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Geo;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Prüfung der Wochenspiele (60-km-Regel): Entfernungen aus Postleitzahlen und Hinweise zu den Sperren.</summary>
public sealed class WochenspielpruefungTests : IDisposable
{
    private static readonly string[] Roemisch = [string.Empty, string.Empty, " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X"];
    private static readonly Spiellokaladresse Mainz = new("Halle Mainz", "Weg 1", "55116", "Mainz");
    private static readonly Spiellokaladresse Berlin = new("Halle Berlin", "Weg 2", "10115", "Berlin");

    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-ws-" + Guid.NewGuid().ToString("N"));

    public WochenspielpruefungTests() => Directory.CreateDirectory(ordner);

    public void Dispose() => Directory.Delete(ordner, recursive: true);

    [Fact]
    public void Ein_Breitengrad_sind_rund_111_km() =>
        Assert.Equal(111.19, new Koordinate(50, 8).Luftlinie(new Koordinate(51, 8)), 1);

    [Fact]
    public void Postleitzahlen_liefern_die_Lage()
    {
        Koordinate mainz = Assert.NotNull(Postleitzahlen.Lage("55116", "Mainz"));
        Koordinate muendersbach = Assert.NotNull(Postleitzahlen.Lage("56271", "Mündersbach"));

        Assert.InRange(mainz.Breite, 49.9, 50.1);
        Assert.InRange(mainz.Laenge, 8.1, 8.4);
        Assert.InRange(mainz.Luftlinie(muendersbach), 70, 110);
        Assert.Null(Postleitzahlen.Lage("00000", string.Empty));
    }

    [Fact]
    public void Ohne_Adressen_wird_nichts_geprueft()
    {
        Datenbearbeitung bearbeitung = Oeffnen(out _);

        Wochenspielpruefung ergebnis = bearbeitung.WochenspielePruefen(60);

        Assert.Equal(0, ergebnis.GeprueftePaare);
        Assert.Empty(ergebnis.Befunde);
        Assert.Equal(bearbeitung.Mannschaftsnamen().Order(StringComparer.Ordinal), ergebnis.OhneLage.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Fehlende_und_unnoetige_Sperren_werden_gemeldet()
    {
        Datenbearbeitung bearbeitung = Oeffnen(out IReadOnlyList<Mannschaft> mannschaften, out string fern, out string nah, out string gast);
        List<Vereinsmeldung> meldungen = mannschaften.Select(m => Meldung(m, m.Name == fern ? Berlin : Mainz)).ToList();

        bearbeitung.NurWochenendeSpeichern(gast, []);
        Wochenspielpruefung vorher = bearbeitung.WochenspielePruefen(60, meldungen);
        bearbeitung.NurWochenendeSpeichern(gast, [fern, nah]);
        Wochenspielpruefung nachher = bearbeitung.WochenspielePruefen(60, meldungen);

        Assert.Empty(vorher.OhneLage);
        Wochenspielbefund fehlt = Assert.Single(vorher.Befunde, b => b.Gast == gast && b.Heim == fern);
        Assert.Equal(Wochenspielhinweis.SperreFehlt, fehlt.Art);
        Assert.InRange(fehlt.Luftlinie, 400, 500);
        Assert.DoesNotContain(vorher.Befunde, b => b.Gast == gast && b.Heim == nah);
        Assert.DoesNotContain(nachher.Befunde, b => b.Gast == gast && b.Heim == fern);
        Assert.Equal(Wochenspielhinweis.SperreUnnoetig, Assert.Single(nachher.Befunde, b => b.Gast == gast && b.Heim == nah).Art);
    }

    [Fact]
    public void Fehlende_Sperren_werden_gesetzt_und_lassen_sich_zuruecknehmen()
    {
        Datenbearbeitung bearbeitung = Oeffnen(out IReadOnlyList<Mannschaft> mannschaften, out string fern, out string nah, out string gast);
        List<Vereinsmeldung> meldungen = mannschaften.Select(m => Meldung(m, m.Name == fern ? Berlin : Mainz)).ToList();
        bearbeitung.NurWochenendeSpeichern(gast, [nah]);

        IReadOnlyList<Wochenspielbefund> gesetzt = bearbeitung.WochenspielSperrenSetzen(bearbeitung.WochenspielePruefen(60, meldungen).Befunde);

        Assert.Contains(gesetzt, b => b.Gast == gast && b.Heim == fern);
        Assert.All(gesetzt, b => Assert.Equal(Wochenspielhinweis.SperreFehlt, b.Art));
        Assert.Equal([nah, fern], bearbeitung.NurWochenendeGegen(gast));
        Assert.DoesNotContain(bearbeitung.WochenspielePruefen(60, meldungen).Befunde, b => b.Art == Wochenspielhinweis.SperreFehlt);

        bearbeitung.WochenspielSperrenEntfernen(gesetzt);

        Assert.Equal([nah], bearbeitung.NurWochenendeGegen(gast));
    }

    [Fact]
    public void Gastgeber_nur_unter_der_Woche_werden_nicht_geprueft()
    {
        Datenbearbeitung bearbeitung = Oeffnen(out IReadOnlyList<Mannschaft> mannschaften, out _, out _, out _);
        List<string> nurWoche = mannschaften
            .Where(m => m.Heimspieltermine.Count > 0 && m.Heimspieltermine.All(t => t.Zeitpunkt.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday))
            .Select(m => m.Name)
            .ToList();
        Assert.NotEmpty(nurWoche);

        Wochenspielpruefung ergebnis = bearbeitung.WochenspielePruefen(60, mannschaften.Select(m => Meldung(m, Berlin)).ToList());

        Assert.DoesNotContain(ergebnis.Wochentagsgastgeber, nurWoche.Contains);
        Assert.DoesNotContain(ergebnis.Befunde, b => nurWoche.Contains(b.Heim));
    }

    [Fact]
    public void Wer_alle_Gegner_sperrt_wird_nicht_bewertet()
    {
        Datenbearbeitung bearbeitung = Oeffnen(out IReadOnlyList<Mannschaft> mannschaften);
        string gast = mannschaften[0].Name;
        bearbeitung.NurWochenendeSpeichern(gast, mannschaften.Skip(1).Select(m => m.Name));

        Wochenspielpruefung ergebnis = bearbeitung.WochenspielePruefen(60, mannschaften.Select(m => Meldung(m, Mainz)).ToList());

        Assert.Contains(gast, ergebnis.PauschalGesperrt);
        Assert.DoesNotContain(ergebnis.Befunde, b => b.Gast == gast);
    }

    [Fact]
    public void Entfernungen_kommen_aus_den_uebernommenen_Adressen()
    {
        Datenbearbeitung bearbeitung = Oeffnen(out IReadOnlyList<Mannschaft> mannschaften);
        Assert.All(bearbeitung.Hallenentfernungen(mannschaften[0].Name), e => Assert.Null(e.Luftlinie));
        string fern = mannschaften[1].Name;
        List<Vereinsmeldung> meldungen = mannschaften.Select(m => Meldung(m, m.Name == fern ? Berlin : Mainz)).ToList();

        bearbeitung.TerminwuenscheUebernehmen(bearbeitung.TerminwuenscheAbgleichen(meldungen), "bericht.pdf");
        IReadOnlyList<Hallenentfernung> entfernungen = bearbeitung.Hallenentfernungen(mannschaften[0].Name);

        Assert.Equal(mannschaften.Count - 1, entfernungen.Count);
        Assert.InRange(entfernungen.Single(e => e.Gegner == fern).Luftlinie ?? 0, 400, 500);
        Assert.All(entfernungen.Where(e => e.Gegner != fern), e => Assert.Equal(0, e.Luftlinie ?? -1, 3));
    }

    private static Vereinsmeldung Meldung(Mannschaft m, Spiellokaladresse halle) => new(
        "Verein",
        int.Parse(m.VereinsId, CultureInfo.InvariantCulture),
        "Erwachsene" + Roemisch[m.Nummer],
        new Dictionary<int, Spiellokaladresse> { [1] = halle, [2] = halle, [3] = halle },
        string.Empty,
        [],
        []);

    /// <summary>Gastgeber mit Heimterminen unter der Woche (Mo–Do) und am Wochenende (Fr–So, Vorgabe der Einstellungen).</summary>
    private static bool WocheUndWochenende(Mannschaft m) =>
        m.Heimspieltermine.Any(t => t.Zeitpunkt.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday)
        && m.Heimspieltermine.Any(t => t.Zeitpunkt.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday);

    private Datenbearbeitung Oeffnen(out IReadOnlyList<Mannschaft> mannschaften, out string fern, out string nah, out string gast)
    {
        Datenbearbeitung bearbeitung = Oeffnen(out mannschaften, "2__Kreisklasse_Gruppe_A (1).xml");
        List<string> beides = mannschaften.Where(WocheUndWochenende).Select(m => m.Name).ToList();
        Assert.True(beides.Count >= 2, "Die Testdaten brauchen zwei Gastgeber mit Heimterminen unter der Woche und am Wochenende.");
        (fern, nah) = (beides[0], beides[1]);
        string f = fern;
        string n = nah;
        gast = mannschaften.First(m => m.Name != f && m.Name != n).Name;
        bearbeitung.NurWochenendeSpeichern(gast, []);
        return bearbeitung;
    }

    private Datenbearbeitung Oeffnen(out IReadOnlyList<Mannschaft> mannschaften, string name = "4__Kreisklasse_Gruppe_A (2).xml")
    {
        string datei = Path.Combine(ordner, "staffel.xml");
        File.Copy(Testdaten.Datei(name), datei);
        Plansitzung sitzung = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis"));
        mannschaften = sitzung.Staffel.Mannschaften;
        return sitzung.DatenBearbeiten();
    }
}
