// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Csv;
using AndiGenerator.Persistence.Gemeinsam;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

public class SpiellokaleTests
{
    private static readonly DateTime Termin = new(2026, 9, 11, 20, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// Wie <see cref="ClickTtCsvExportTests.VollstaendigTerminiertePlaene"/>, aber mit dem Datenstand, aus dem die CSV
    /// tatsächlich exportiert wurde: „2. Kreisklasse.csv“ stammt aus „2__Kreisklasse (7)“ (die Spiellokale wurden
    /// danach in „(8)“ wieder entfernt; beide click-TT-Dateien sind identisch).
    /// </summary>
    /// <returns>Paare aus CSV-Datei und click-TT-Datei.</returns>
    public static TheoryData<string, string> Exportstaende() => new()
    {
        { "1. Kreisklasse Gruppe A.csv", "1__Kreisklasse_Gruppe_A (1).xml" },
        { "1. Kreisklasse Gruppe B.csv", "1__Kreisklasse_Gruppe_B (1).xml" },
        { "1. Kreisklasse.csv", "1__Kreisklasse (4).xml" },
        { "2. Kreisklasse Gruppe A.csv", "2__Kreisklasse_Gruppe_A (1).xml" },
        { "2. Kreisklasse Gruppe B.csv", "2__Kreisklasse_Gruppe_B (1).xml" },
        { "2. Kreisklasse.csv", "2__Kreisklasse (7).xml" },
        { "3. Kreisklasse.csv", "3__Kreisklasse (11).xml" },
        { "4. Kreisklasse.csv", "4__Kreisklasse (5).xml" },
        { "4. Kreisklasse A.csv", "4__Kreisklasse (5).xml" },
        { "4. Kreisklasse Gruppe A.csv", "4__Kreisklasse_Gruppe_A (2).xml" },
        { "4. Kreisklasse Gruppe B.csv", "4__Kreisklasse_Gruppe_B (2).xml" },
        { "Bezirksliga Rheinhessen Nord.csv", "Bezirksliga_Rheinhessen_Nord (1).xml" },
        { "Bezirksliga Rheinhessen Süd.csv", "Bezirksliga_Rheinhessen_Süd (3).xml" },
        { "Bezirksoberliga Rheinhessen Nord.csv", "Bezirksoberliga_Rheinhessen_Nord (1).xml" },
        { "Bezirksoberliga Rheinhessen Süd.csv", "Bezirksoberliga_Rheinhessen_Süd (2).xml" },
        { "Damen Bezirksliga Rheinhessen.csv", "Damen_Bezirksliga_Rheinhessen (1).xml" },
        { "Damen Bezirksoberliga Rheinhessen.csv", "Damen_Bezirksoberliga_Rheinhessen (1).xml" },
    };

    [Theory]
    [MemberData(nameof(Exportstaende))]
    public void Csv_mit_ermittelten_Spiellokalen_ist_byte_gleich(string csvDatei, string xmlDatei)
    {
        byte[] original = File.ReadAllBytes(Testdaten.Datei(csvDatei));
        ClickTtStaffel staffel = ClickTtLeser.Lesen(Testdaten.Datei(xmlDatei));

        DatenKnoten stand = ClickTtNachPlanDaten.Erzeugen(staffel);
        string modifikationen = Path.ChangeExtension(Testdaten.Datei(xmlDatei), ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        var lokale = new Spiellokale(stand);
        List<CsvSpiel> spiele = DelphiKompatibel.Windows1252.GetString(original)
            .Split("\r\n")
            .Skip(1)
            .Where(z => z.Length > 0)
            .Select(z => z.Split(';'))
            .Select(f =>
            {
                DateTime termin = DateTime.ParseExact(f[4] + " " + f[6], "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
                return new CsvSpiel(termin, f[8], f[10], lokale.Ermitteln(termin, f[8], f[10]));
            })
            .ToList();

        var optionen = new CsvExportOptionen(
            Staffelbeginn: staffel.Von.ToDateTime(TimeOnly.MinValue),
            Rueckrundenbeginn: staffel.Rueckrundenbeginn!.Value.ToDateTime(TimeOnly.MinValue),
            Halbrunde: false,
            IstInZuPlanenderRunde: _ => true);
        byte[] neu = ClickTtCsvExport.ErzeugenBytes(spiele, staffel.Mannschaften.Select(m => new CsvMannschaft(m.Name, m.VereinsId, m.Id)), optionen);

        Assert.Equal(DelphiKompatibel.Windows1252.GetString(original), DelphiKompatibel.Windows1252.GetString(neu));
    }

    [Theory]
    [InlineData("3", "", "3")]
    [InlineData("3", "2", "2")]
    [InlineData("", "4", "4")]
    [InlineData("3", "6", "3")]
    [InlineData(" 5 ", "x", "5")]
    [InlineData("", "", "")]
    public void Heimspieltermin_vor_Mannschaft(string mannschaft, string termin, string erwartet)
    {
        DatenKnoten plan = Plan(mannschaft);
        Heimtermin(plan, "11.09.2026 20:00", termin);

        Assert.Equal(erwartet, new Spiellokale(plan).Ermitteln(Termin, "A", "B"));
    }

    [Fact]
    public void Ohne_passenden_Heimspieltermin_oder_Termin_leer()
    {
        DatenKnoten plan = Plan("3");
        Heimtermin(plan, "11.09.2026 19:30");
        var lokale = new Spiellokale(plan);

        Assert.Equal(string.Empty, lokale.Ermitteln(Termin, "A", "B"));
        Assert.Equal(string.Empty, lokale.Ermitteln(null, "A", "B"));
        Assert.Equal(string.Empty, lokale.Ermitteln(Termin, "B", "A"));
        Assert.Equal(string.Empty, lokale.Ermitteln(Termin, "Unbekannt", "A"));
    }

    [Fact]
    public void Vorgegebenes_Spiel_gewinnt_auch_mit_leerem_Lokal()
    {
        DatenKnoten plan = Plan("3");
        Heimtermin(plan, "11.09.2026 20:00", "2");
        var vorgaben = new DatenKnoten("predefinedgames", plan);
        var spiel = new DatenKnoten("game", vorgaben);
        spiel.Setzen("datetime", "11.09.2026 20:00");
        spiel.Setzen("hometeamname", "A");
        spiel.Setzen("guestteamname", "B");
        spiel.Setzen("location", string.Empty);
        var lokale = new Spiellokale(plan);

        Assert.Equal(string.Empty, lokale.Ermitteln(Termin, "A", "B"));

        spiel.Setzen("location", "4");
        Assert.Equal("4", new Spiellokale(plan).Ermitteln(Termin, "A", "B"));
    }

    [Theory]
    [InlineData("couplegameday")]
    [InlineData("coupleauswaertssecondtime")]
    public void Zweite_Uhrzeit_hat_dasselbe_Lokal(string art)
    {
        DatenKnoten plan = Plan("1");
        DatenKnoten heim = Heimtermin(plan, "11.09.2026 16:00", "2");
        heim.Setzen(art, true);
        heim.Setzen("couplesecondtime", "20:00");

        Assert.Equal("2", new Spiellokale(plan).Ermitteln(Termin, "A", "B"));
    }

    [Fact]
    public void Halbrunde_ignoriert_Termine_ausserhalb_des_Zeitraums()
    {
        DatenKnoten plan = Plan("3");
        Heimtermin(plan, "09.05.2027 20:00");
        Heimtermin(plan, "09.05.2027 00:00");
        var termin = new DateTime(2027, 5, 9, 20, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("3", new Spiellokale(plan).Ermitteln(termin, "A", "B"));
        Assert.Equal(string.Empty, new Spiellokale(plan, halbrunde: true).Ermitteln(termin, "A", "B"));
        Assert.Equal("3", new Spiellokale(plan, halbrunde: true).Ermitteln(new DateTime(2027, 5, 9, 0, 0, 0, DateTimeKind.Unspecified), "A", "B"));
    }

    [Fact]
    public void Ungueltiger_Termin_im_Datenbaum_wird_gemeldet()
    {
        DatenKnoten plan = Plan();
        Heimtermin(plan, "31.02.2027 20:00");
        Assert.Throws<PlanDatenFormatException>(() => new Spiellokale(plan));
    }

    private static DatenKnoten Plan(string mannschaftsLokal = "")
    {
        var plan = new DatenKnoten("plan");
        plan.Setzen("from", "17.08.2026");
        plan.Setzen("until", "09.05.2027");
        var team = new DatenKnoten("team", plan);
        team.Setzen("teamname", "A");
        team.Setzen("location", mannschaftsLokal);
        new DatenKnoten("team", plan).Setzen("teamname", "B");
        return plan;
    }

    private static DatenKnoten Heimtermin(DatenKnoten plan, string termin, string lokal = "")
    {
        var heim = new DatenKnoten("homegameday", plan.Kinder[0]);
        heim.Setzen("datetime", termin);
        heim.Setzen("location", lokal);
        return heim;
    }
}
