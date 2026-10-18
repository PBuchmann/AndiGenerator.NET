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
using AndiGenerator.Engine.Turniere;
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

    /// <summary>Die Kriterien, die das Modell der Teilneuplanung genau zählt (nach der Reihenfolge der eigenen Spiele).</summary>
    private static readonly Kostenkriterium[] Genau =
    [
        Kostenkriterium.Spieltaglaenge, Kostenkriterium.Spieltagueberlappung, Kostenkriterium.LetzterSpieltagLaenge,
        Kostenkriterium.LetzterSpieltagUeberlappung, Kostenkriterium.AbstandHeimAuswaerts, Kostenkriterium.Setzliste,
    ];

    /// <summary>
    /// Die Setzliste hat im Original keine Meldungen; hier je verletztem Spiel eine (für die Einzelheiten in der Qualitätsansicht
    /// und die Unterschiede der B-Varianten) – so viele, wie die Bewertung zählt.
    /// </summary>
    [Fact]
    public void Setzliste_meldet_jedes_verletzte_Spiel()
    {
        // Der Plan von R21 ohne Rücksicht auf eine Setzliste; die Mannschaften in ihrer Reihenfolge als Setzliste.
        Staffel r21 = Laden("R21_Verbandsoberliga (1).xml", mitPlan: true);
        Staffel staffel = r21 with { Setzliste = new Setzliste(true, [.. r21.Mannschaften.Select((m, i) => new Setzlisteneintrag(m.Name, i))]) };
        Planbewertung bewertung = Referenzbewertung.Bewerten(staffel, Berechnungsoptionen.Standard);
        IReadOnlyList<Mannschaftsmeldungen> meldungen = Referenzbewertung.MeldungenJeMannschaft(staffel, Berechnungsoptionen.Standard, MannschaftsKostenart.Setzliste);

        Assert.Equal(bewertung.Mannschaften.Select(m => Math.Max(0, m.JeKostenart[(int)MannschaftsKostenart.Setzliste].Anzahl)), meldungen.Select(m => m.Meldungen.Count));
        Assert.Contains(meldungen, m => m.Meldungen.Count > 0);
        Assert.All(meldungen.SelectMany(m => m.Meldungen), t => Assert.Matches(@"^Setzliste: .+ gegen .+ \(\d+ Plätze entfernt\) ist das \d+\. Spiel der Runde – erlaubt bis zum \d+\.$", t));
    }

    /// <summary>
    /// Zeitgrenze nach Fortschritt: HiGHS liest im Rückruf besten Plan und Untergrenze; bei R3 ist jede Stufe schnell bewiesen –
    /// wie mit fester Zeit (<see cref="R3_Schranke_wird_vom_Plan_erreicht"/>).
    /// </summary>
    [HighsFact]
    public void Zeit_nach_Fortschritt_beweist_wie_mit_fester_Zeit()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml");
        var regel = new Laufzeitregel(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(15));

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), Kette, zeitStufeC: TimeSpan.FromSeconds(1), laufzeit: regel);

        Assert.Equal(["A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4"], bericht.Stufen.Where(s => s.Stufe[0] != 'C').Select(s => s.Stufe));
        Assert.All(bericht.Stufen.Where(s => s.Stufe[0] != 'C'), s => Assert.True(s.Bewiesen));
    }

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
        Assert.All(bericht.Stufen.Where(s => s.Stufe[0] != 'C'), s => Assert.True(s.Bewiesen));

        // Stufe C (bei bestmöglichem A und B): alles 0, Wechsel H/A ist nicht dabei. Ob HiGHS C in den 5 s beweist, hängt von der
        // Last des Rechners ab (im vollen Testlauf oder neben einer laufenden Generierung oft nicht) – geprüft wird es nicht.
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
    /// Welche Kriterien das Modell nicht kennt: bei Hin- und Rückrunde keines (auch Abstand H/A, Setzliste und Pausen sind
    /// modelliert), bei der Doppelrunde Abstand H/A und vereinsinterne Spiele am Anfang (R23 hat keine Setzliste); bei der
    /// Corona-Rückrunde gibt es kein Modell.
    /// </summary>
    [Fact]
    public void Unbekannte_Kriterien_je_Staffel()
    {
        Staffel staffel = Laden("R11_4__Kreisklasse (5).xml");
        Staffel doppel = Laden("R23_Liga für Doppelrunde.xml");

        Assert.Equal([], Ligaschranke.Unbekannt(staffel, Berechnungsoptionen.Standard));
        Assert.Equal(
            [Kostenkriterium.AbstandHeimAuswaerts, Kostenkriterium.VereinsinterneSpieleAmAnfang],
            Ligaschranke.Unbekannt(doppel, Berechnungsoptionen.Standard with { Doppelrunde = true }));
        Assert.Null(Ligaschranke.Unbekannt(staffel, Berechnungsoptionen.Standard with { Rundenplanung = Rundenplanung.Corona }));
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
    /// Das Modell der Schranke zählt Spieltage, Abstand Heim/Auswärts, Setzliste und die Pausen der Spielverteilung genau wie
    /// die Bewertung (Spieltage zu lang und überlappend, auch der letzte) – mit Hin- und Rückrunde (R21, R26: mit
    /// Koppelterminen, die Pausen verkürzen) und mit „nur Rückrunde“.
    /// </summary>
    /// <param name="datei">Die Staffel mit Plan.</param>
    /// <param name="rueckrunde">Nur Rückrunde (die Vorrunde des Plans ist fest).</param>
    [HighsTheory]
    [InlineData("R11_4__Kreisklasse (5).xml", false)]
    [InlineData("R16_Bezirksoberliga_Rheinhessen_Nord (1).xml", false)]
    [InlineData("R21_Verbandsoberliga (1).xml", false)]
    [InlineData("R26_Liga mit Koppelterminen.xml", true)]
    [InlineData("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml", false)]
    [InlineData("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml", true)]
    public void Reihenfolge_im_Modell_wie_in_der_Bewertung(string datei, bool rueckrunde)
    {
        Staffel staffel = Laden(datei, mitPlan: true);
        Berechnungsoptionen optionen = rueckrunde ? Berechnungsoptionen.Standard with { Rundenplanung = Rundenplanung.NurRueckrunde } : Berechnungsoptionen.Standard;
        Kostenkriterium[] kriterien = [.. Genau, Kostenkriterium.Spielverteilung];

        Dictionary<Kostenkriterium, int>? modell = Ligaschranke.ImModellZaehlen(staffel, staffel.BestehenderSpielplan, optionen, new Highsbruecke(), kriterien);

        Assert.NotNull(modell);
        Assert.Equal(Echt(staffel, optionen, kriterien), kriterien.Select(k => modell.GetValueOrDefault(k, -1)));
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

        // Vorn der Plan für die Summe in B (bewiesen: „Plan B optimal“), dann die Varianten mit Vorrang (allein bewiesen). Welche
        // es gibt, hängt davon ab, welchen Plan der Summe HiGHS in der Zeit findet (unter Last ein anderer) – mit Vorrang für
        // parallele Spiele ist meist eine dabei.
        IReadOnlyList<Planalternative> varianten = bericht.Alternativen ?? [];
        Assert.Null(varianten[0].Kriterium);
        Assert.Equal("Plan B optimal", varianten[0].Name);
        Assert.All(varianten, a => Assert.True(a.Bewiesen));
        Assert.All(varianten.Skip(1), a => Assert.Contains(a.Kriterium!.Value, einteilung.B));

        // Schon vor der Stufe C kommt ein Bericht mit A, B und den Varianten (zum Wählen, während C rechnet).
        Schrankenbericht vorC = Assert.Single(zwischen);
        Assert.Equal((0, 8), (vorC.SchrankeA, vorC.SchrankeB));
        Assert.DoesNotContain(vorC.Stufen, x => x.Stufe[0] == 'C');
        Assert.Equal("Plan B optimal", (vorC.Alternativen ?? [])[0].Name);
        foreach (Planalternative alternative in varianten.Skip(1))
        {
            Planbewertung b = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = alternative.Plan }, Berechnungsoptionen.Standard);
            Assert.Equal(0, b.NichtTerminiert + b.UngueltigeSpiele + Anzahl(b, MannschaftsKostenart.Hallenbelegung));
            Assert.True(einteilung.Zaehlen(b).B >= 8);
            if (alternative.Kriterium == Kostenkriterium.ParalleleSpiele)
            {
                Assert.Equal(3, Anzahl(b, MannschaftsKostenart.ParalleleSpiele));
            }
        }
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

    /// <summary>Die Verstöße je Kriterium nach der Bewertung (Spieltage aus den Zusatzzahlen).</summary>
    private static IEnumerable<int> Echt(Staffel staffel, Berechnungsoptionen optionen, IEnumerable<Kostenkriterium> kriterien)
    {
        Planbewertung b = Referenzbewertung.Bewerten(staffel, optionen);
        Zusatzverstoesse z = b.Zusatz!;
        return kriterien.Select(k => k switch
        {
            Kostenkriterium.Spieltaglaenge => z.SpieltageLaenge,
            Kostenkriterium.Spieltagueberlappung => z.SpieltageUeberlappung,
            Kostenkriterium.LetzterSpieltagLaenge => z.LetzterLaenge,
            Kostenkriterium.LetzterSpieltagUeberlappung => z.LetzterUeberlappung,
            Kostenkriterium.Spielverteilung => z.Spielverteilung.Sum(),
            _ => b.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)k].Anzahl)),
        });
    }

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
