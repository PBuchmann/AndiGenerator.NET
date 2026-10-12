// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Bericht „Terminwünsche“ aus click-TT: Textzeilen auswerten, mit den Spielplandaten abgleichen und übernehmen.</summary>
public sealed class TerminwuenscheTests : IDisposable
{
    private static readonly string Privat = Path.Combine(
        typeof(TerminwuenscheTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "Loesungsordner").Value!,
        "Referenz");

    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-tw-" + Guid.NewGuid().ToString("N"));

    public TerminwuenscheTests() => Directory.CreateDirectory(ordner);

    public void Dispose() => Directory.Delete(ordner, recursive: true);

    [Fact]
    public void Bericht_wird_aus_Textzeilen_gelesen()
    {
        // Aufbau wie nu.Dokument 019 (erfundene Vereine); Kopf und Fuß jeder Seite werden übergangen.
        double oben = 30;
        var zeilen = new List<Textzeile>();
        void Zeile(params (double Links, string Text)[] woerter)
        {
            zeilen.Add(new Textzeile(0, oben, woerter.Select(w => new Textwort(w.Links, w.Text)).ToList()));
            oben += 12;
        }

        void Text(string text) => Zeile((28, text));
        Text("Tischtennisverband Rheinland Rheinhessen e.V.");
        Text("Bezirksliga Test");
        Text("Terminwünsche");
        oben = 100;
        Text("Verein Nord (000123) - Erwachsene II");
        Text("Kontaktperson");
        Text("Max Muster, Weg 1, 11111 Irgendwo,");
        Text("Spiellokal 1");
        Text("Sporthalle Nord, Hauptstraße 5, 12345 Nordstadt,");
        Text("Tel.: 01234 5678");
        Text("Spiellokal 2");
        Text("Alte Halle, 12345 Nordstadt-Süd,");
        Text("Spiellokal 3");
        Text("Zusatzinformationen");
        Text("Ball: Plastik weiß");
        Text("Bemerkung: Bitte nicht vor");
        Text("18 Uhr ansetzen.");
        Text("Terminwünsche Vorrunde");
        Zeile((28, "Spieltag"), (150, "Datum"), (280, "Spiellokal"), (400, "max."), (430, "Heimspiele"), (500, "Heimkoppelspiel"));
        Zeile((28, "1."), (40, "Spieltag"), (150, "Sa"), (165, "05.09.2026"), (215, "18:00"), (245, "Uhr"), (280, "Sporthalle"), (330, "Nord"), (400, "2"), (500, "wenn"));
        Zeile((500, "möglich"));
        Zeile((28, "2."), (40, "Spieltag"), (150, "Fr"), (165, "11.09.2026"), (215, "20:00"), (245, "Uhr"), (280, "Alte"), (400, "unendlich"), (500, "-"));
        Zeile((280, "Halle"));
        Text("Sperrtermine");
        Text("12.09.2026, 13.09.2026,");
        Text("27.09.2026");
        Text("nu.Dokument 019, erstellt am 12.10.2026 10:48 | Seite 1 von 2");
        Text("Terminwünsche Rückrunde");
        Text("Keine Terminwünsche vorhanden.");
        Text("Sperrtermine");
        Text("Verein Süd (77) - Jugend 19");
        Text("Spiellokal 1");
        Text("Halle Süd, 54321 Südstadt");

        IReadOnlyList<Vereinsmeldung> meldungen = TerminwunschBericht.Lesen(zeilen);

        Assert.Equal(2, meldungen.Count);
        Vereinsmeldung nord = meldungen[0];
        Assert.Equal(("Verein Nord", 123, "Erwachsene II", 2), (nord.Verein, nord.VereinsNummer, nord.Klasse, nord.MannschaftsNummer));
        Assert.Equal(new Spiellokaladresse("Sporthalle Nord", "Hauptstraße 5", "12345", "Nordstadt"), nord.Spiellokale[1]);
        Assert.Equal(new Spiellokaladresse("Alte Halle", string.Empty, "12345", "Nordstadt-Süd"), nord.Spiellokale[2]);
        Assert.False(nord.Spiellokale.ContainsKey(3));
        Assert.Equal("Bitte nicht vor 18 Uhr ansetzen.", nord.Bemerkung);
        Assert.Equal(
            [
                new GemeldeterHeimtermin(new DateTime(2026, 9, 5, 18, 0, 0, DateTimeKind.Unspecified), "Sporthalle Nord", "2", "wenn möglich"),
                new GemeldeterHeimtermin(new DateTime(2026, 9, 11, 20, 0, 0, DateTimeKind.Unspecified), "Alte Halle", "unendlich", "-"),
            ],
            nord.Heimtermine);
        Assert.Equal([new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 27)], nord.Sperrtermine);
        Assert.Equal(("Verein Süd", 77, 1), (meldungen[1].Verein, meldungen[1].VereinsNummer, meldungen[1].MannschaftsNummer));
        Assert.Equal("Halle Süd", meldungen[1].Spiellokale[1].Name);
    }

    [Fact]
    public void Anderer_Text_wird_abgelehnt() =>
        Assert.Throws<FormatException>(() => TerminwunschBericht.Lesen([new Textzeile(0, 120, [new Textwort(28, "Spielplan")])]));

    [Fact]
    public void Abgleich_setzt_Spiellokal_Nummern_aus_dem_Hallennamen()
    {
        string datei = Path.Combine(ordner, "staffel.xml");
        File.Copy(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"), datei);
        Plansitzung sitzung = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis"));
        Mannschaft erste = sitzung.Staffel.Mannschaften.First(m => m.Heimspieltermine.Any(t => t.Halle.Length > 0));
        string halle = erste.Heimspieltermine.First(t => t.Halle.Length > 0).Halle;
        var meldung = new Vereinsmeldung(
            "Verein",
            int.Parse(erste.VereinsId, System.Globalization.CultureInfo.InvariantCulture),
            erste.Nummer > 1 ? "Erwachsene " + new string('I', erste.Nummer) : "Erwachsene",
            new Dictionary<int, Spiellokaladresse> { [1] = new("Andere Halle", "Weg 1", "11111", "A"), [2] = new(halle.ToUpperInvariant(), "Weg 2", "22222", "B") },
            "Bitte samstags.",
            erste.Heimspieltermine.Select(t => new GemeldeterHeimtermin(t.Zeitpunkt, t.Halle, "2", "-")).ToList(),
            erste.Sperrtermine);
        Datenbearbeitung bearbeitung = sitzung.DatenBearbeiten();

        Terminwunschabgleich abgleich = bearbeitung.TerminwuenscheAbgleichen([meldung]);

        Mannschaftsabgleich ergebnis = abgleich.Mannschaften.Single(m => m.Gefunden);
        Assert.Equal(erste.Name, ergebnis.Mannschaft);
        Assert.Empty(ergebnis.Abweichungen);
        Assert.Equal("Bitte samstags.", ergebnis.Bemerkung);
        int mitHalle = erste.Heimspieltermine.Count(t => t.Halle == halle && t.Spiellokal.Length == 0);
        Assert.Equal(mitHalle, abgleich.NummernGesetzt);

        bearbeitung.TerminwuenscheUebernehmen(abgleich, "bericht.pdf");
        sitzung.DatenUebernehmen(bearbeitung);

        Assert.All(
            sitzung.Staffel.Mannschaften.Single(m => m.Name == erste.Name).Heimspieltermine.Where(t => t.Halle == halle),
            t => Assert.Equal("2", t.Spiellokal));
        Assert.StartsWith("„bericht.pdf“ übernommen am ", sitzung.DatenBearbeiten().TerminwunschVermerk, StringComparison.Ordinal);
    }

    [Fact]
    public void Abweichungen_zu_den_Spielplandaten_werden_gemeldet()
    {
        string datei = Path.Combine(ordner, "staffel.xml");
        File.Copy(Testdaten.Datei("4__Kreisklasse_Gruppe_A (2).xml"), datei);
        Plansitzung sitzung = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis"));
        Mannschaft erste = sitzung.Staffel.Mannschaften[0];
        var meldung = new Vereinsmeldung(
            "Verein",
            int.Parse(erste.VereinsId, System.Globalization.CultureInfo.InvariantCulture),
            erste.Nummer > 1 ? "Erwachsene " + new string('I', erste.Nummer) : "Erwachsene",
            new Dictionary<int, Spiellokaladresse>(),
            string.Empty,
            [new GemeldeterHeimtermin(new DateTime(2030, 1, 5, 18, 0, 0, DateTimeKind.Unspecified), "Halle", "2", "-")],
            [new DateOnly(2030, 1, 6)]);

        Mannschaftsabgleich ergebnis = sitzung.DatenBearbeiten().TerminwuenscheAbgleichen([meldung]).Mannschaften.Single(m => m.Gefunden);

        Assert.Contains("Heimtermin nur im Bericht: Sa 05.01.2030 18:00", ergebnis.Abweichungen);
        Assert.Contains("Sperrtermin nur im Bericht: So 06.01.2030", ergebnis.Abweichungen);
        Assert.Equal(erste.Heimspieltermine.Count, ergebnis.Abweichungen.Count(a => a.StartsWith("Heimtermin nur in den Spielplandaten", StringComparison.Ordinal)));
    }

    /// <summary>Echte Berichte (privat, nicht im Repository): Sie stimmen mit der click-TT-Datei derselben Staffel überein.</summary>
    [Fact]
    public void Echte_Berichte_passen_zur_click_TT_Datei()
    {
        string berichte = Path.Combine(Privat, "terminwuensche");
        if (!Directory.Exists(berichte))
        {
            return;
        }

        foreach ((string pdf, string xml) in new[] { ("Bezirksoberliga Rheinhessen Nord", "R16_"), ("Damen Bezirksoberliga Rheinhessen", "R3_"), ("gesamt Verbandsoberliga", "R21_") })
        {
            string bericht = Path.Combine(berichte, "Terminmeldung_" + pdf + ".pdf");
            string[] staffeln = Directory.Exists(Path.Combine(Privat, "eingabe")) ? Directory.GetFiles(Path.Combine(Privat, "eingabe"), xml + "*.xml") : [];
            if (!File.Exists(bericht) || staffeln.Length != 1)
            {
                continue;
            }

            string datei = Path.Combine(ordner, pdf + ".xml");
            File.Copy(staffeln[0], datei);
            Terminwunschabgleich abgleich = Plansitzung.Oeffnen(datei, Path.Combine(ordner, "basis")).DatenBearbeiten().TerminwuenscheAbgleichen(TerminwunschPdf.Laden(bericht));

            Assert.All(abgleich.Mannschaften, m => Assert.True(m.Gefunden, pdf + ": " + m.Mannschaft));
            Assert.All(abgleich.Mannschaften, m => Assert.True(m.Abweichungen.Count == 0, pdf + ": " + m.Mannschaft + ": " + string.Join("; ", m.Abweichungen)));
            Assert.True(abgleich.NummernGesetzt > 0, pdf);
        }
    }
}
