// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using AndiGenerator.Application;
using AndiGenerator.Domain.Geo;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Geo;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Kompaktspieltage: Kurznamen, „Nur Heim“ aus der Bemerkung, Kartendaten und Schalter der Staffel.</summary>
public sealed class KompaktspieltageTests : IDisposable
{
    private static readonly string Privat = Path.Combine(
        typeof(KompaktspieltageTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "Loesungsordner").Value!,
        "Referenz");

    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-kompakt-" + Guid.NewGuid().ToString("N"));

    public KompaktspieltageTests() => Directory.CreateDirectory(ordner);

    public void Dispose() => Directory.Delete(ordner, recursive: true);

    [Theory]
    [InlineData("ESV Engers", "Engers")]
    [InlineData("SG Sinzig/Ehlingen", "Sinzig")]
    [InlineData("TTG Mündersbach/Höchstenbach", "Mündersbach")]
    [InlineData("RSV Klein-Winternheim II", "Klein-Winternheim")]
    [InlineData("DJK SV RW Finthen", "Finthen")]
    [InlineData("TTC", "TTC")]
    public void Kurzname_ohne_Vereinskuerzel(string name, string kurz) => Assert.Equal(kurz, Kompaktplanung.Kurzname(name));

    [Theory]
    [InlineData("Jugend 19 Verbandsoberliga (HR)", Halbrundenkennzeichen.Hinrunde)]
    [InlineData("Jungen 15 Rheinlandliga (FR) Gruppe 2", Halbrundenkennzeichen.Rueckrunde)]
    [InlineData("Jugend 13 Kreisliga ( hr )", Halbrundenkennzeichen.Hinrunde)]
    [InlineData("Herren Verbandsliga", Halbrundenkennzeichen.Keins)]
    [InlineData("Herren HR-Liga", Halbrundenkennzeichen.Keins)]
    public void Halbrunde_am_Klassennamen(string name, Halbrundenkennzeichen kennzeichen) =>
        Assert.Equal(kennzeichen, Halbrundenname.Kennzeichen(name));

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(9, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(12, 6, true)]
    [InlineData(10, 6, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 2, false)]
    public void Wochenende_ueberwiegt_trotz_einzelner_Werktage(int wochenende, int werktage, bool erwartet) =>
        Assert.Equal(erwartet, Kompaktplanung.WochenendeUeberwiegt(wochenende, werktage));

    [Fact]
    public void Nur_Heim_liest_nur_die_Daten_direkt_hinter_dem_Stichwort()
    {
        DateOnly[] tage = [new(2026, 10, 18), new(2026, 11, 1), new(2026, 12, 6), new(2027, 3, 7)];

        IReadOnlyList<DateOnly> nurHeim = Kompaktplanung.NurHeim("Bitte nicht vor 11 Uhr. Nur Heim: 18.10., 1.11.2026 und 07.03. Prio 6.12.2026 für den Heimspieltag", tage);

        Assert.Equal([new DateOnly(2026, 10, 18), new DateOnly(2026, 11, 1), new DateOnly(2027, 3, 7)], nurHeim);
        Assert.Empty(Kompaktplanung.NurHeim("Heimspiele bitte erst ab 12 Uhr", tage));
    }

    [Fact]
    public void Kartendaten_enthalten_die_Bundeslaender_und_Staedte()
    {
        Assert.Equal(16, Kartendaten.Laender.Count);
        Assert.Contains(Kartendaten.Laender, l => l.Name == "Rheinland-Pfalz" && l.Ringe[0].Count > 50);
        Stadt koblenz = Assert.Single(Kartendaten.Staedte, s => s.Name == "Koblenz");
        Assert.InRange(koblenz.Lage.Breite, 50.3, 50.4);
        Assert.Equal(1, Kartendaten.Staedte[0].Rang);
    }

    [Fact]
    public void Schalter_wird_mit_der_Staffel_gespeichert()
    {
        Plansitzung sitzung = Oeffnen(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"));
        Datenbearbeitung daten = sitzung.DatenBearbeiten();
        Assert.Equal(Kompaktmodus.Offen, daten.Kompaktspieltage);

        daten.KompaktspieltageSetzen(Kompaktmodus.Ein);
        sitzung.DatenUebernehmen(daten);

        Assert.Equal(Kompaktmodus.Ein, sitzung.DatenBearbeiten().Kompaktspieltage);
        Assert.Equal(Kompaktmodus.Ein, Plansitzung.Oeffnen(sitzung.Pfad, Path.Combine(ordner, "basis")).DatenBearbeiten().Kompaktspieltage);
    }

    [Fact]
    public void Plan_der_Jugend_Verbandsoberliga_wird_als_Kompaktspieltage_erkannt()
    {
        string quelle = Path.Combine(Privat, "jugend", "Jugend_19_Verbandsoberliga_HR.xml");
        if (!File.Exists(quelle))
        {
            return; // Echte Daten liegen nur im privaten Ordner „Referenz“.
        }

        Plansitzung sitzung = Oeffnen(quelle);

        Kompaktauswertung a = Kompaktplanung.Auswerten(sitzung.Staffel, sitzung.DatenBearbeiten(), Rundenplanung.Beide, new DateOnly(2026, 7, 1));

        Assert.Equal(9, a.Mannschaften.Count);
        Assert.Equal(9, a.Spieltage.Count);
        Assert.All(a.Spieltage, s => Assert.Equal(Turnierform.Vierer, s.Form));
        Assert.Equal(36, a.Spieltage.Sum(s => s.Spiele.Count));
        Assert.All(a.Tage, t => Assert.Equal(DayOfWeek.Sunday, t.DayOfWeek));
        Assert.True(a.Tage[0] < new DateOnly(2026, 9, 1) && a.Tage[^1] < new DateOnly(2026, 12, 7));
        Assert.Equal(8, a.MeisteAmTag);
        Assert.True(Kompaktplanung.InFrage(sitzung.Staffel, sitzung.Optionen.Rundenplanung, false));

        // „(HR)“ im Namen: nur die Hinrunde, ohne Frage und ohne Meldung als abweichende Einstellung.
        Assert.Equal(Rundenplanung.NurVorrunde, sitzung.Optionen.Rundenplanung);
        Assert.Equal(Rundenvorschlag.Keiner, sitzung.RundeVorschlagen(new DateOnly(2026, 11, 20)));
        Assert.False(sitzung.OptionenWeichenVomStandardAb);
        Assert.Empty(sitzung.AbweichungenVomStandard());

        sitzung.OptionenSpeichern(sitzung.Optionen with { Rundenplanung = Rundenplanung.Beide });
        sitzung.StandardoptionenFuerDieseSitzung();

        Assert.Equal(Rundenplanung.NurVorrunde, sitzung.Optionen.Rundenplanung);
    }

    private Plansitzung Oeffnen(string quelle)
    {
        string datei = Path.Combine(ordner, Path.GetFileName(quelle));
        File.Copy(quelle, datei);
        return Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis"));
    }
}
