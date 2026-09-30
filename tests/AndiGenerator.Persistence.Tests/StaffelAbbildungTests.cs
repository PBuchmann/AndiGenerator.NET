// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

public class StaffelAbbildungTests
{
    private static readonly DateTime Termin = new(2026, 9, 11, 20, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Alle_click_TT_Importe_ueberstehen_den_Weg_ueber_das_Fachmodell()
    {
        int geprueft = 0;
        foreach (string datei in Directory.GetFiles(Testdaten.Ordner, "*.xml").Order(StringComparer.Ordinal))
        {
            if (!ClickTtLeser.IstClickTtDatei(datei))
            {
                continue;
            }

            AssertUnveraendert(ClickTtNachPlanDaten.Laden(datei), Path.GetFileName(datei));
            geprueft++;
        }

        Assert.True(geprueft >= 40, $"Nur {geprueft} click-TT-Dateien geprüft.");
    }

    [Theory]
    [MemberData(nameof(PlanDatenTests.AlleModifikationen), MemberType = typeof(PlanDatenTests))]
    public void Staende_mit_Aenderungen_ueberstehen_den_Weg_ueber_das_Fachmodell(string modifikationen, string xml)
    {
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(Testdaten.Datei(xml));
        stand.Zusammenfuehren(PlanDatenDatei.Laden(Testdaten.Datei(modifikationen)));

        AssertUnveraendert(stand, modifikationen);
    }

    [Fact]
    public void Fachmodell_enthaelt_die_Daten_des_Imports()
    {
        ClickTtStaffel quelle = ClickTtLeser.Lesen(Testdaten.Datei("Verbandsoberliga (1).xml"));
        Staffel staffel = StaffelAbbildung.AusPlanDaten(ClickTtNachPlanDaten.Erzeugen(quelle));

        Assert.Equal(quelle.Name, staffel.Name);
        Assert.Equal(quelle.Id, staffel.Id);
        Assert.Equal(quelle.Von, staffel.Beginn);
        Assert.Equal(quelle.Bis, staffel.Ende);
        Assert.Equal(quelle.Rueckrundenbeginn, staffel.Rueckrundenbeginn);
        Assert.Equal(quelle.Mannschaften.Select(m => m.Name), staffel.Mannschaften.Select(m => m.Name));
        Assert.Equal(quelle.Mannschaften.Select(m => m.Id), staffel.Mannschaften.Select(m => m.Id));
        Assert.All(staffel.Mannschaften, m => Assert.Equal(-1, m.HeimspieleHinrunde));
        Assert.Equal(
            quelle.Mannschaften.Select(m => m.Nachbarmannschaften.Count),
            staffel.Mannschaften.Select(m => m.Nachbarmannschaften.Count));
        Assert.Equal(quelle.BestehenderSpielplan.Count, staffel.BestehenderSpielplan.Count);
    }

    [Fact]
    public void Alle_Felder_ueberstehen_Schreiben_und_Lesen()
    {
        Staffel staffel = VollstaendigeStaffel();

        DatenKnoten baum = StaffelAbbildung.NachPlanDaten(staffel);
        using var datei = new MemoryStream(PlanDatenDatei.ErzeugenBytes(baum));
        Staffel gelesen = StaffelAbbildung.AusPlanDaten(PlanDatenDatei.Laden(datei));

        Assert.Equal(baum.Kanonisch(), StaffelAbbildung.NachPlanDaten(gelesen).Kanonisch());
        Mannschaft a = gelesen.Mannschaften[0];
        Assert.Equal("TTC Alt", a.UrspruenglicherName);
        Assert.Equal(Terminkoppelung.Koppeltermin, a.Heimspieltermine[1].Koppelung);
        Assert.Equal(Koppelprioritaet.Hart, a.Heimspieltermine[1].Prioritaet);
        Assert.Equal(new TimeOnly(16, 0), a.Heimspieltermine[1].ZweiteUhrzeit);
        Assert.Equal(Auswaertskoppelart.Beliebig, a.Auswaertskoppeln[0].Art);
        Assert.Equal(Runde.Rueckrunde, a.Heimrechte[0].Runde);
        Assert.Equal("2", a.Nachbarmannschaften[0].Spiele[0].Spiellokal);
        Assert.Equal(3, gelesen.Pflichtspielzeitraeume[0].AnzahlSpiele);
        Assert.True(gelesen.Setzliste!.Aktiv);
        Assert.Equal(1, gelesen.Setzliste.Eintraege[1].Platz);
        Assert.Null(gelesen.VorgegebeneSpiele[1].Zeitpunkt);
    }

    [Fact]
    public void Leere_Staffel_ergibt_leeren_Plan()
    {
        DatenKnoten baum = StaffelAbbildung.NachPlanDaten(Staffel.Leer);

        Assert.True(baum.IstGleich(new DatenKnoten("plan")));
        Staffel gelesen = StaffelAbbildung.AusPlanDaten(new DatenKnoten("plan"));
        Assert.Null(gelesen.Beginn);
        Assert.Null(gelesen.Setzliste);
        Assert.Empty(gelesen.Mannschaften);
    }

    [Fact]
    public void Lesen_folgt_den_Eigenheiten_des_Originals()
    {
        var plan = new DatenKnoten("plan");
        var team = new DatenKnoten("team", plan);
        team.Setzen("teamname", "A");
        team.Setzen("location", "9");
        var heim = new DatenKnoten("homegameday", team);
        heim.Setzen("datetime", "11.09.2026 20:00");
        heim.Setzen("couplegameday", true);
        heim.Setzen("doublegameday", true);
        heim.Setzen("coupleprio", "7");
        heim.Setzen("location", " 3 ");
        var koppel = new DatenKnoten("roadcouple", team);
        koppel.Setzen("teamnamea", "B");
        koppel.Setzen("teamnameb", "C");
        koppel.Setzen("sameday", "5");
        var recht = new DatenKnoten("homerights", team);
        recht.Setzen("teamname", "B");
        recht.Setzen("homeright", "3");

        Mannschaft a = StaffelAbbildung.AusPlanDaten(plan).Mannschaften[0];

        Assert.Equal(string.Empty, a.Spiellokal);
        Assert.Equal(0, a.Nummer);
        Assert.Equal(Termin, a.Heimspieltermine[0].Zeitpunkt);
        Assert.Equal(Terminkoppelung.Koppeltermin, a.Heimspieltermine[0].Koppelung);
        Assert.Equal(Koppelprioritaet.Moeglich, a.Heimspieltermine[0].Prioritaet);
        Assert.Null(a.Heimspieltermine[0].ZweiteUhrzeit);
        Assert.Equal("3", a.Heimspieltermine[0].Spiellokal);
        Assert.Equal(Auswaertskoppelart.Beliebig, a.Auswaertskoppeln[0].Art);
        Assert.Empty(a.Heimrechte);
    }

    [Theory]
    [InlineData("homegameday", "datetime", "31.02.2027 20:00")]
    [InlineData("homegameday", "couplesecondtime", "25:00")]
    [InlineData("nogameday", "date", "1.1.2027")]
    public void Ungueltige_Termine_werden_gemeldet(string knoten, string attribut, string wert)
    {
        var plan = new DatenKnoten("plan");
        var team = new DatenKnoten("team", plan);
        team.Setzen("teamname", "A");
        var kind = new DatenKnoten(knoten, team);
        kind.Setzen(attribut == "couplesecondtime" ? "datetime" : "date", "11.09.2026 20:00");
        kind.Setzen(attribut, wert);

        Assert.Throws<PlanDatenFormatException>(() => StaffelAbbildung.AusPlanDaten(plan));
    }

    private static void AssertUnveraendert(DatenKnoten original, string bezeichnung)
    {
        // Eine leere Liste vorgegebener Spiele bzw. eines bestehenden Spielplans ist gleichbedeutend mit einer fehlenden
        // (Original TPlan.load); das Fachmodell kennt den Unterschied nicht und schreibt leere Listen nicht.
        DatenKnoten vergleich = original.Kopie();
        vergleich.Kinder.RemoveAll(k => k.Name is "predefinedgames" or "existingschedule" && k.Kinder.Count == 0);

        DatenKnoten neu = StaffelAbbildung.NachPlanDaten(StaffelAbbildung.AusPlanDaten(vergleich));
        if (!neu.IstGleich(vergleich))
        {
            DatenKnoten differenz = DatenKnoten.Differenz(neu, vergleich);
            Assert.Fail($"{bezeichnung}: Abweichung nach dem Weg über das Fachmodell:\n{differenz.Kanonisch()}");
        }
    }

    private static Staffel VollstaendigeStaffel()
    {
        var termine = new List<Heimspieltermin>
        {
            new(Termin, 2, true, "1", Terminkoppelung.Keine, Koppelprioritaet.Moeglich, null, false),
            new(Termin.AddDays(7), 0, false, string.Empty, Terminkoppelung.Koppeltermin, Koppelprioritaet.Hart, new TimeOnly(16, 0), false),
            new(Termin.AddDays(14), 0, false, string.Empty, Terminkoppelung.Doppelspieltag, Koppelprioritaet.Weich, null, false),
            new(Termin.AddDays(21), 0, false, string.Empty, Terminkoppelung.Keine, Koppelprioritaet.Moeglich, new TimeOnly(15, 30), true),
        };
        var nachbar = new Nachbarmannschaft(
            "TTC Neu II",
            "Herren",
            2,
            "4",
            true,
            false,
            [new Nachbarspiel(Termin.AddHours(-2), "TTC Neu II", "SV X", "2"), new Nachbarspiel(Termin.AddDays(3), "SV X", "TTC Neu II", string.Empty)]);
        var a = new Mannschaft(
            "TTC Neu",
            "TTC Alt",
            "100",
            "10",
            1,
            "3",
            4,
            termine,
            [new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 25)],
            [new Auswaertskoppel("B", "C", Auswaertskoppelart.Beliebig), new Auswaertskoppel("C", "B", Auswaertskoppelart.AmSelbenTag)],
            [new Heimrechtvorgabe("B", Runde.Rueckrunde), new Heimrechtvorgabe("C", Runde.Hinrunde)],
            ["B"],
            [nachbar]);
        var b = new Mannschaft("B", string.Empty, "200", "20", 1, string.Empty, -1, [], [], [], [], [], []);
        return new Staffel(
            "Testliga",
            "4711",
            "Herren",
            new DateOnly(2026, 8, 17),
            new DateOnly(2027, 5, 9),
            new DateOnly(2026, 12, 7),
            [a, b],
            [new Spiel(Termin, "TTC Neu", "B", "5"), new Spiel(null, "B", "TTC Neu", string.Empty)],
            [new DateOnly(2026, 10, 3)],
            [new Pflichtspielzeitraum(new DateOnly(2027, 4, 13), new DateOnly(2027, 4, 15), 3)],
            new Setzliste(true, [new Setzlisteneintrag("B", 0), new Setzlisteneintrag("TTC Neu", 1)]),
            [new Spiel(Termin.AddDays(1), "B", "TTC Neu", string.Empty)]);
    }
}
