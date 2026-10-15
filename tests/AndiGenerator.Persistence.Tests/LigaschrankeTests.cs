// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Untere Schranke der Stufe A mit HiGHS (MIGRATIONSPLAN O7, Schritt 1). Allein (nicht neben anderen rechenintensiven Tests):
/// Unter Last beweist HiGHS eine Stufe sonst nicht rechtzeitig.
/// </summary>
[Collection(MessungenSammlung.Name)]
public sealed class LigaschrankeTests
{
    /// <summary>Zeitgrenze je Stufe der Kette (endet, sobald die Stufe bewiesen ist – meist nach Sekundenbruchteilen).</summary>
    private static readonly TimeSpan Kette = TimeSpan.FromSeconds(60);

    /// <summary>Zeitgrenze für Stufe C und Feinschliff (wird meist ganz genutzt).</summary>
    private static readonly TimeSpan StufeC = TimeSpan.FromSeconds(5);

    /// <summary>
    /// R3: Zwei parallele Spiele sind unvermeidbar, und der Plan des Modells erreicht genau das – gültig und mit denselben
    /// Zahlen nach der echten Kostenfunktion. Damit ist Stufe A dort beweisbar bestmöglich.
    /// </summary>
    [HighsFact]
    public void R3_Schranke_wird_vom_Plan_erreicht()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml");

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), Kette, zeitStufeC: StufeC);

        Assert.All(bericht.Hinweise, h => Assert.True(h.StartsWith("Feinschliff", StringComparison.Ordinal) || h.StartsWith("Startplan", StringComparison.Ordinal), h));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4"], bericht.Stufen.Where(s => s.Stufe[0] != 'C').Select(s => s.Stufe));
        Assert.All(bericht.Stufen, s => Assert.True(s.Bewiesen));

        // Stufe C (bei bestmöglichem A und B): alles 0, Wechsel H/A ist nicht dabei.
        Assert.All(bericht.Stufen.Where(s => s.Stufe[0] == 'C'), s => Assert.Equal(0, s.Schranke));
        Assert.DoesNotContain(bericht.Stufen, s => s.Kriterium == Kostenkriterium.WechselHeimAuswaerts);
        Assert.Equal([0, 0, 0, 2, 0, 0, 0, 0, 0], bericht.Stufen.Where(s => s.Stufe[0] != 'C').Select(s => s.Schranke));
        Assert.Equal((2, 0), (bericht.SchrankeA, bericht.SchrankeB));

        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = bericht.Plan }, Berechnungsoptionen.Standard);
        Assert.Equal(0, b.NichtTerminiert + b.UngueltigeSpiele + b.SpieleAnSpielfreienTagen);
        Assert.Equal(0, Anzahl(b, MannschaftsKostenart.Hallenbelegung));
        Assert.Equal(2, Anzahl(b, MannschaftsKostenart.ParalleleSpiele));
        Assert.Equal(0, Anzahl(b, MannschaftsKostenart.Pflichtspieltage));
        Assert.Equal(0, b.VereinsinterneSpieleAmAnfang);

        // Feinschliff: Er macht die gewichteten C-Verstöße nicht mehr (wie weit er kommt, hängt von der Last des Rechners ab –
        // im vollen Testlauf teilt er sich die Kerne; allein erreicht er bei R3 0).
        string fein = Assert.Single(bericht.Hinweise, h => h.StartsWith("Feinschliff (", StringComparison.Ordinal));
        string[] teile = fein[(fein.IndexOf(':', StringComparison.Ordinal) + 1)..].TrimEnd('.').Split('→', StringSplitOptions.TrimEntries);
        Assert.True(int.Parse(teile[1], CultureInfo.InvariantCulture) <= int.Parse(teile[0], CultureInfo.InvariantCulture), fein);
    }

    /// <summary>
    /// R11: Stufe A zählt als Summe (wie im Automodus) – die Reihenfolge innerhalb von A ändert die Schranke nicht. Allein lassen
    /// sich die vereinsinternen Spiele an den Anfang legen; zusammen sind drei A-Verstöße unvermeidbar.
    /// </summary>
    [HighsFact]
    public void R11_Stufe_A_zaehlt_als_Summe()
    {
        Staffel staffel = Laden("R11_4__Kreisklasse (5).xml");
        Stufeneinteilung hinten = Stufeneinteilung.Standard.Platzieren(Kostenkriterium.VereinsinterneSpieleAmAnfang, Kostenkriterium.Pflichtspieltage, davor: false);

        // Die Summe von B ist hier schwer zu beweisen; geprüft wird nur A (kurze Zeitgrenze).
        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), TimeSpan.FromSeconds(10), hinten, zeitStufeC: StufeC);

        Assert.Equal(3, bericht.SchrankeA);
        Assert.Equal(0, bericht.Stufen.Single(s => s.Kriterium == Kostenkriterium.VereinsinterneSpieleAmAnfang).Einzeln);
        Assert.All(bericht.Stufen.Where(s => s.Stufe[0] == 'A'), s => Assert.True(s.Einzeln <= 3));
        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = bericht.Plan }, Berechnungsoptionen.Standard);
        Assert.Equal(0, b.NichtTerminiert + b.UngueltigeSpiele);
        Assert.Equal(3, hinten.Zaehlen(b).A);
    }

    /// <summary>Halbrunde: je Paarung ein Spiel in beliebiger Richtung; R08 hat zwei unvermeidbare A-Verstöße.</summary>
    [HighsFact]
    public void Halbrunde_R08()
    {
        Staffel staffel = Laden("R08_2__Kreisklasse_Gruppe_B (1).xml");
        Berechnungsoptionen optionen = Berechnungsoptionen.Standard with { Rundenplanung = Rundenplanung.Halbrunde };

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, optionen, new Highsbruecke(), Kette, zeitStufeC: StufeC);

        // Allein ist jedes A-Kriterium vermeidbar, zusammen sind zwei Verstöße unvermeidbar.
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0, 0], bericht.Stufen.Where(s => s.Stufe[0] != 'C').Select(s => s.Schranke));
        Assert.Equal((2, 0), (bericht.SchrankeA, bericht.SchrankeB));
        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = bericht.Plan }, optionen);
        Assert.Equal(0, b.NichtTerminiert + b.UngueltigeSpiele);
        Assert.Equal(staffel.Mannschaften.Count * (staffel.Mannschaften.Count - 1) / 2, bericht.Plan.Count);
        Assert.Equal(2, Stufeneinteilung.Standard.Zaehlen(b).A);
    }

    /// <summary>Nur Rückrunde: Die Vorrunde des bestehenden Plans bleibt fest, die Rückspiele haben ihre Richtung.</summary>
    [HighsFact]
    public void Rueckrunde_haelt_die_Vorrunde_fest()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml", mitPlan: true);
        Berechnungsoptionen optionen = Berechnungsoptionen.Standard with { Rundenplanung = Rundenplanung.NurRueckrunde };

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, optionen, new Highsbruecke(), Kette, zeitStufeC: StufeC);

        Assert.Equal([0, 0, 0, 2, 0, 0, 0, 0, 0], bericht.Stufen.Where(s => s.Stufe[0] != 'C').Select(s => s.Schranke));
        Assert.Equal(72, bericht.Plan.Count);
        Assert.All(bericht.Plan, s => Assert.Contains(staffel.BestehenderSpielplan, v => (v.Heim == s.Gast && v.Gast == s.Heim) || (v.Heim == s.Heim && v.Gast == s.Gast && v.Zeitpunkt == s.Zeitpunkt)));
        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = bericht.Plan }, optionen);
        Assert.Equal(0, b.NichtTerminiert + b.UngueltigeSpiele);
        Assert.Equal(2, Anzahl(b, MannschaftsKostenart.ParalleleSpiele));
    }

    /// <summary>Die Corona-Rückrunde wird nicht unterstützt; der Grund steht in den Hinweisen.</summary>
    [Fact]
    public void Corona_wird_nicht_berechnet()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml");

        Schrankenbericht bericht = Ligaschranke.Berechnen(
            staffel,
            Berechnungsoptionen.Standard with { Rundenplanung = Rundenplanung.Corona },
            new Highsbruecke(),
            TimeSpan.FromSeconds(1));

        Assert.Empty(bericht.Stufen);
        Assert.Single(bericht.Hinweise);
    }

    /// <summary>
    /// Teilneuplanung (O7 Schritt 3): Ausschnitte des click-TT-Plans werden neu geplant; A und B werden nicht schlechter, C nicht
    /// mehr, und der Bericht bewertet den Plan, den er liefert.
    /// </summary>
    [HighsFact]
    public void Neuplanen_macht_A_und_B_nicht_schlechter()
    {
        Staffel staffel = Laden("R11_4__Kreisklasse (5).xml", mitPlan: true);
        var einstellungen = new Neuplanungseinstellungen(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(2), Startwert: 3);

        Neuplanungsbericht bericht = Ligaschranke.Neuplanen(staffel, staffel.BestehenderSpielplan, Berechnungsoptionen.Standard, new Highsbruecke(), Stufeneinteilung.Standard, einstellungen);

        Assert.True(bericht.Schritte > 0);
        Assert.True(bericht.Nachher.Harte <= bericht.Vorher.Harte && bericht.Nachher.A <= bericht.Vorher.A && bericht.Nachher.B <= bericht.Vorher.B);
        Assert.True(bericht.Nachher.CAnzahl <= bericht.Vorher.CAnzahl);
        Assert.Equal(staffel.BestehenderSpielplan.Count, bericht.Plan.Count);
        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = bericht.Plan }, Berechnungsoptionen.Standard);
        Assert.Equal(bericht.Nachher, Stufeneinteilung.Standard.Zaehlen(b));
    }

    /// <summary>
    /// Zwei Spiele einer Mannschaft am selben Tag (Koppeltermin) passen ins Modell der Teilneuplanung: Die genäherten Pausen
    /// verlangten dafür früher eine Hilfsvariable ≥ 2 – „Der Plan passt nicht ins Modell“.
    /// </summary>
    [HighsFact]
    public void Neuplanen_nimmt_Plaene_mit_Koppelterminen_an()
    {
        Staffel staffel = Laden("R21_Verbandsoberliga (1).xml", mitPlan: true);
        Assert.Contains(staffel.BestehenderSpielplan.Where(s => s.Zeitpunkt is not null).GroupBy(s => (s.Heim, s.Zeitpunkt!.Value.Date)), g => g.Count() > 1);
        var einstellungen = new Neuplanungseinstellungen(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), Startwert: 3);

        Neuplanungsbericht bericht = Ligaschranke.Neuplanen(staffel, staffel.BestehenderSpielplan, Berechnungsoptionen.Standard, new Highsbruecke(), Stufeneinteilung.Standard, einstellungen);

        Assert.True(bericht.Schritte > 0, string.Join(Environment.NewLine, bericht.Protokoll));
    }

    /// <summary>
    /// Alternativen in B: R16 mit parallelen Spielen und Sperrterminen in B. Der Plan der Summe hat 4 parallele Spiele und 4
    /// Sperrtermine; allein gehen 3 parallele Spiele – die Alternative hält sie und zahlt mit einem Sperrtermin mehr. Die Summe B
    /// beginnt mit der besten Alternative und ist bewiesen.
    /// </summary>
    [HighsFact]
    public void Alternativen_in_B_halten_ein_Kriterium_bestmoeglich()
    {
        Staffel staffel = Laden("R16_Bezirksoberliga_Rheinhessen_Nord (1).xml");
        Stufeneinteilung einteilung = Stufeneinteilung.AusText("A:Hallenbelegung;B:ParalleleSpiele,Sperrtermine,Auswaertskoppel,Heimkoppel,Ausweichtermine,Pflichtspieltage");

        var zwischen = new List<Schrankenbericht>();
        void Melden(Schrankenfortschritt f)
        {
            if (f.Zwischenbericht is { } z)
            {
                zwischen.Add(z);
            }
        }

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), Kette, einteilung, Melden, StufeC);

        Assert.Equal((0, 8), (bericht.SchrankeA, bericht.SchrankeB));

        // Vorn der Plan für die Summe in B (bewiesen: „Plan B optimal“), dann die Variante mit Vorrang für parallele Spiele
        // (allein bewiesen).
        IReadOnlyList<Planalternative> varianten = bericht.Alternativen ?? [];
        Assert.Equal([null, Kostenkriterium.ParalleleSpiele], varianten.Select(x => x.Kriterium));
        Assert.Equal("Plan B optimal", varianten[0].Name);
        Assert.All(varianten, a => Assert.True(a.Bewiesen));

        // Schon vor der Stufe C kommt ein Bericht mit A, B und den Varianten (zum Wählen, während C rechnet).
        Schrankenbericht vorC = Assert.Single(zwischen);
        Assert.Equal((0, 8), (vorC.SchrankeA, vorC.SchrankeB));
        Assert.DoesNotContain(vorC.Stufen, x => x.Stufe[0] == 'C');
        Assert.Equal("Plan B optimal", (vorC.Alternativen ?? [])[0].Name);
        Planalternative alternative = varianten[1];
        Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = alternative.Plan }, Berechnungsoptionen.Standard);
        Assert.Equal(0, b.NichtTerminiert + b.UngueltigeSpiele + Anzahl(b, MannschaftsKostenart.Hallenbelegung));
        Assert.Equal(3, Anzahl(b, MannschaftsKostenart.ParalleleSpiele));
        Assert.True(einteilung.Zaehlen(b).B >= 8);
    }

    /// <summary>
    /// Startlösung: Mit dem Plan einer ersten Rechnung als bestem Plan der Generierung kommt dieselbe Schranke heraus, und der
    /// Plan ist in A und B nicht schlechter. Die Schranke sinkt bei einer neuen Rechnung desselben Modells nie (Gedächtnis).
    /// </summary>
    [HighsFact]
    public void Startplan_der_Generierung_wird_genutzt()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml");
        Schrankenbericht erste = Ligaschranke.Berechnen(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), Kette, zeitStufeC: StufeC);
        int gefragt = 0;

        Schrankenbericht zweite = Ligaschranke.Berechnen(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), Kette, zeitStufeC: StufeC, startplan: () =>
        {
            gefragt++;
            return erste.Plan;
        });

        Assert.Equal(3, gefragt);
        Assert.Equal((erste.SchrankeA, erste.SchrankeB), (zweite.SchrankeA, zweite.SchrankeB));
        Assert.All(zweite.Stufen.Zip(erste.Stufen), p => Assert.True(p.First.Schranke >= p.Second.Schranke, p.First.Stufe));
        Stufeneinteilung e = Stufeneinteilung.Standard.Vervollstaendigt();
        Stufenwert vorher = e.Zaehlen(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = erste.Plan }, Berechnungsoptionen.Standard));
        Stufenwert nachher = e.Zaehlen(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = zweite.Plan }, Berechnungsoptionen.Standard));
        Assert.True((nachher.Harte, nachher.A, nachher.B).CompareTo((vorher.Harte, vorher.A, vorher.B)) <= 0, $"{vorher} → {nachher}");
    }

    private static int Anzahl(Planbewertung b, MannschaftsKostenart art) => b.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)art].Anzahl));

    private static Staffel Laden(string datei, bool mitPlan = false)
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string aenderungen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(aenderungen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(aenderungen));
        }

        Staffel staffel = StaffelAbbildung.AusPlanDaten(stand);
        return mitPlan ? staffel : staffel with { BestehenderSpielplan = [] };
    }
}
