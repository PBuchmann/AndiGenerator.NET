// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Xml.Linq;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

public class GespeicherterPlanTests
{
    [Fact]
    public void DateiformatEntsprichtSaveScheduleToXml()
    {
        GespeichertesSpiel[] spiele =
        [
            new(new DateTime(2026, 9, 12, 18, 30, 0, DateTimeKind.Unspecified), "TTC B", "TTC A"),
            new(null, "TTC A", "TTC B"),
            new(new DateTime(2026, 9, 12, 18, 30, 0, DateTimeKind.Unspecified), "TTC A", "TTC C"),
            new(new DateTime(2026, 9, 5, 9, 5, 0, DateTimeKind.Unspecified), " TTC C ", "TTC B"),
        ];

        string text = Encoding.UTF8.GetString(GespeicherterPlanDatei.ErzeugenBytes(spiele));

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<andigenerator><plan><schedule>" +
            "<game datetime=\"30.12.1899 00:00\" hometeamname=\"TTC A\" guestteamname=\"TTC B\"/>" +
            "<game datetime=\"05.09.2026 09:05\" hometeamname=\"TTC C\" guestteamname=\"TTC B\"/>" +
            "<game datetime=\"12.09.2026 18:30\" hometeamname=\"TTC A\" guestteamname=\"TTC C\"/>" +
            "<game datetime=\"12.09.2026 18:30\" hometeamname=\"TTC B\" guestteamname=\"TTC A\"/>" +
            "</schedule></plan></andigenerator>\r\n",
            text);
    }

    [Fact]
    public void BestehendeSpielplaeneUeberstehenSpeichernUndLaden()
    {
        int geprueft = 0;
        foreach (string datei in Directory.GetFiles(Testdaten.Ordner, "*.xml").Order(StringComparer.Ordinal))
        {
            if (!ClickTtLeser.IstClickTtDatei(datei))
            {
                continue;
            }

            ClickTtStaffel staffel = ClickTtLeser.Lesen(datei);
            if (staffel.BestehenderSpielplan.Count == 0)
            {
                continue;
            }

            var spiele = staffel.BestehenderSpielplan.Select(s => new GespeichertesSpiel(s.Zeitpunkt, s.Heim, s.Gast)).ToList();
            byte[] inhalt = GespeicherterPlanDatei.ErzeugenBytes(spiele);
            using var strom = new MemoryStream(inhalt);
            IReadOnlyList<GespeichertesSpiel> geladen = GespeicherterPlanDatei.Laden(strom);

            Assert.Equal(spiele.Count, geladen.Count);
            Assert.Equal(
                spiele.Select(s => (s.Zeitpunkt, s.Heim, s.Gast)).Order(),
                geladen.Select(s => (s.Zeitpunkt, s.Heim, s.Gast)).Order());
            Assert.Equal(inhalt, GespeicherterPlanDatei.ErzeugenBytes(geladen));
            geprueft++;
        }

        Assert.True(geprueft >= 1, $"Nur {geprueft} Dateien mit bestehendem Spielplan.");
    }

    [Fact]
    public void NullpunktUndLeererTerminBedeutenNichtTerminiert()
    {
        IReadOnlyList<GespeichertesSpiel> spiele = LadenAus(
            "<andigenerator><plan><schedule>" +
            "<game datetime=\"30.12.1899 00:00\" hometeamname=\"A\" guestteamname=\"B\"/>" +
            "<game hometeamname=\"B\" guestteamname=\"A\"/>" +
            "<game datetime=\"30.12.1899 12:00\" hometeamname=\"A\" guestteamname=\"C\"/>" +
            "</schedule></plan></andigenerator>");

        Assert.Null(spiele[0].Zeitpunkt);
        Assert.Null(spiele[1].Zeitpunkt);
        Assert.Equal(new DateTime(1899, 12, 30, 12, 0, 0, DateTimeKind.Unspecified), spiele[2].Zeitpunkt);
    }

    [Theory]
    [InlineData("07.11.2026")]
    [InlineData("31.02.2026 18:00")]
    [InlineData("07.11.2026 24:00")]
    [InlineData("xx.11.2026 18:00")]
    public void UngueltigerTerminMachtDenPlanUngueltig(string termin)
    {
        Assert.Throws<PlanDatenFormatException>(() => LadenAus(
            $"<andigenerator><plan><schedule><game datetime=\"{termin}\" hometeamname=\"A\" guestteamname=\"B\"/></schedule></plan></andigenerator>"));
    }

    [Fact]
    public void OhneScheduleIstDerPlanLeer()
    {
        Assert.Empty(LadenAus("<andigenerator><plan/></andigenerator>"));
    }

    [Theory]
    [InlineData("Plan A", "Plan A")]
    [InlineData("A/B: 1#2?", "A#047B#058 1#0352#063")]
    [InlineData("<\"[]\\*|>", "#060#034#091#093#092#042#124#062")]
    public void DateinamenWerdenWieImOriginalKodiert(string name, string kodiert)
    {
        Assert.Equal(kodiert, Staffelordner.DateinameKodieren(name));
        Assert.Equal(name, Staffelordner.DateinameDekodieren(kodiert));
    }

    [Theory]
    [InlineData("#12", "#12")]
    [InlineData("a#b12", "a#b12")]
    [InlineData("#0651", "A1")]
    [InlineData("x# 65", "xA")]
    public void DekodierenLaesstUngueltigeFolgenStehen(string kodiert, string name)
    {
        Assert.Equal(name, Staffelordner.DateinameDekodieren(kodiert));
    }

    [Fact]
    public void StaffelordnerAusNameUndJahr()
    {
        string basis = Path.Combine("C:", "Basis");
        Assert.Equal(
            Path.Combine(basis, "Andi-Generator", "Verbandsliga Rheinland Süd-West 2026"),
            Staffelordner.Pfad(basis, "Verbandsliga Rheinland Süd-West", new DateOnly(2026, 8, 1)));

        // Ohne Namen und Beginn legt das Original den Ordner " 1899" an.
        Assert.Equal(Path.Combine(basis, "Andi-Generator", " 1899"), Staffelordner.Pfad(basis, string.Empty, null));
    }

    [Fact]
    public void StaffelordnerPassenZuDenGesichertenOptionsdateien()
    {
        var gesichert = Directory.GetFiles(Testdaten.Datei("Optionen (Stand 27.09.2026 19 Uhr)"), "*.options")
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .ToHashSet(StringComparer.Ordinal);

        var ordnerNamen = Directory.GetFiles(Testdaten.Ordner, "*.xml")
            .Where(d => ClickTtLeser.IstClickTtDatei(d))
            .Select(d => ClickTtLeser.Lesen(d))
            .Select(s => Path.GetFileName(Staffelordner.Pfad("basis", s.Name, s.Von)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Verbandsliga Rheinland Süd-West 2026", ordnerNamen);
        Assert.True(gesichert.Intersect(ordnerNamen).Count() >= 10, "Zu wenige Staffelordner stimmen mit den gesicherten Optionsdateien überein.");
    }

    [Fact]
    public void GemerktePlaeneNeuesteZuerstMitDekodiertemNamen()
    {
        string ordner = Path.Combine(Path.GetTempPath(), "andigen-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            string alt = Staffelordner.GemerkterPlanPfad(ordner, "Alt: Variante 1");
            string neu = Staffelordner.GemerkterPlanPfad(ordner, "Neu");
            GespeicherterPlanDatei.Speichern(alt, []);
            GespeicherterPlanDatei.Speichern(neu, []);
            File.SetLastWriteTime(alt, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Local));
            File.SetLastWriteTime(neu, new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Local));
            File.WriteAllText(Path.Combine(ordner, "AndiGenerator.options"), "x");

            IReadOnlyList<GemerkterPlanEintrag> plaene = Staffelordner.GemerktePlaene(ordner);

            Assert.Equal(new[] { "Neu", "Alt: Variante 1" }, plaene.Select(p => p.Name));
            Assert.EndsWith("Alt#058 Variante 1.xml", plaene[1].Pfad, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(ordner, recursive: true);
        }
    }

    [Theory]
    [InlineData("Testplan.xml", 110, 0)]
    [InlineData("Testplan2.xml", 0, 110)]
    public void GemerkterPlanDesOriginalsWirdInhaltsgleichGeschrieben(string datei, int ohneTermin, int mitTermin)
    {
        // Mit "Plan merken" im Original (26.7.1.0) erzeugt. Die Attributreihenfolge folgt im Original der
        // internen Hash-Reihenfolge (TDictionary) und ist für das Format ohne Bedeutung; alles andere muss gleich sein.
        string pfad = Path.Combine(Testdaten.Datei("Gemerkte Plaene"), "1. Kreisklasse 2026", datei);
        byte[] original = File.ReadAllBytes(pfad);

        IReadOnlyList<GespeichertesSpiel> spiele = GespeicherterPlanDatei.Laden(pfad);
        byte[] neu = GespeicherterPlanDatei.ErzeugenBytes(spiele);

        Assert.Equal(ohneTermin, spiele.Count(s => s.Zeitpunkt is null));
        Assert.Equal(mitTermin, spiele.Count(s => s.Zeitpunkt is not null));
        Assert.Equal(original.Length, neu.Length);
        Assert.Equal(Normalisiert(original), Normalisiert(neu));
        Assert.Equal("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<andigenerator><plan><schedule><game ", Encoding.UTF8.GetString(neu, 0, 77));
        Assert.EndsWith("</schedule></plan></andigenerator>\r\n", Encoding.UTF8.GetString(original), StringComparison.Ordinal);
    }

    private static IReadOnlyList<GespeichertesSpiel> LadenAus(string xml)
    {
        using var strom = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return GespeicherterPlanDatei.Laden(strom);
    }

    /// <summary>Elemente in Dokumentreihenfolge, Attribute alphabetisch.</summary>
    private static string[] Normalisiert(byte[] inhalt)
    {
        using var strom = new MemoryStream(inhalt);
        return XDocument.Load(strom).Descendants()
            .Select(e => e.Name.LocalName + "|" + string.Join("|", e.Attributes().Select(a => a.Name.LocalName + "=" + a.Value).Order(StringComparer.Ordinal)))
            .ToArray();
    }
}
