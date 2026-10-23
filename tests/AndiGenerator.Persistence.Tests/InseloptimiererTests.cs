// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Kern;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Tests des Inselmodells (MIGRATIONSPLAN E6).</summary>
public class InseloptimiererTests
{
    private const string R2 = "R2_4__Kreisklasse_Gruppe_A (2).xml";

    [Fact]
    public void Rechnen_ist_mit_Startwert_reproduzierbar()
    {
        Staffel staffel = LeereStaffel(R2);
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 42 };

        Optimierungsergebnis a = Rechnen(staffel, einstellungen, 20_000);
        Optimierungsergebnis b = Rechnen(staffel, einstellungen, 20_000);

        Assert.Equal(a.Kosten, b.Kosten);
        Assert.Equal(a.Verbesserungen, b.Verbesserungen);
        Assert.Equal(a.Spiele, b.Spiele);
    }

    [Theory]
    [InlineData(R2)]
    [InlineData("R26_Liga mit Koppelterminen.xml")]
    [InlineData("R25_Liga mit 2 Mannschaften aus einem Verein.xml")]
    [InlineData("R24_Liga mit 11 Mannschaften und 40km Regel.xml")]
    public void Rechnen_liefert_einen_gueltigen_besseren_Plan(string datei)
    {
        Staffel staffel = LeereStaffel(datei);
        double start = Kernbewertung.Bewerten(staffel, Berechnungsoptionen.Standard).Gesamtkosten;

        Optimierungsergebnis ergebnis = Rechnen(staffel, Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 7 }, 30_000);

        Assert.True(ergebnis.Kosten < start, $"{ergebnis.Kosten} >= {start}");
        PlanPruefen(staffel, ergebnis);
    }

    [Theory]
    [InlineData(R2)]
    [InlineData("R26_Liga mit Koppelterminen.xml")]
    public void Automodus_liefert_reproduzierbar_einen_gueltigen_Plan_nicht_schlechter_als_der_Grundlauf(string datei)
    {
        // Ausgangspunkt ist ein schon optimierter Plan: Die Basisoptimierung endet erst, wenn die Suche steht; aus einem
        // leeren Plan findet sie in einem kurzen, reproduzierbaren Lauf laufend Verbesserungen.
        Staffel leer = LeereStaffel(datei);
        Optimierungseinstellungen original = Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 37 };
        Staffel staffel = leer with { BestehenderSpielplan = Rechnen(leer, original, 60_000).Spiele };
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with
        {
            Kerne = 1,
            Startwert = 37,
            DurchlaeufeVorNeustart = 2_000,
            Automodus = true,
            AutoStillstandPlaene = 2_000,

            // Der Automodus beobachtet Sekunden ohne Verbesserung; bei Rechnen entspricht der ganze Lauf dem Zeitbudget. Ein
            // großes Budget lässt zwischen zwei Abgleichen genug „Zeit“, damit Grundlauf und Lenkung in wenigen Durchläufen ablaufen.
            Zeitbudget = 2_000,
        };

        (Optimierungsergebnis a, Lenkungsstand? lenkungA) = MitLenkung(staffel, einstellungen, 80_000);
        (Optimierungsergebnis b, Lenkungsstand? lenkungB) = MitLenkung(staffel, einstellungen, 80_000);

        Assert.NotNull(lenkungA);
        Assert.NotNull(lenkungB);
        Stufenwert basis = Assert.IsType<Stufenwert>(lenkungA.Basis);
        Assert.False(basis.IstBesserAls(lenkungA.Bester), $"{lenkungA.Bester} schlechter als {basis}");
        Assert.Equal(a.Kosten, b.Kosten);
        Assert.Equal(a.Spiele, b.Spiele);
        Assert.Equal(lenkungA.Aenderungen, lenkungB.Aenderungen);
        PlanPruefen(staffel, a);

        // Für die Ergebniskachel: in Stufe C genau das Kriterium, das geglättet wird.
        Assert.NotNull(lenkungA.Kriterien);
        Assert.True(lenkungA.Bezeichnung != "Optimierung Stufe C" || lenkungA.Kriterien.Count == 1);

        // Die Oberfläche zählt die Verstöße aus der Bewertung des Plans genauso wie der Automodus.
        Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = a.Spiele }, Berechnungsoptionen.Standard);
        Stufenwert gezaehlt = Stufeneinteilung.Standard.Zaehlen(bewertung);
        Assert.Equal((lenkungA.Bester.A, lenkungA.Bester.B, lenkungA.Bester.CAnzahl), (gezaehlt.A, gezaehlt.B, gezaehlt.CAnzahl));

        // Ändert der Staffelleiter während des Laufs die Einteilung, gibt es keine neue Basisoptimierung: Der Automodus macht
        // beim wichtigsten geänderten Kriterium weiter: Auswärtskoppel von B1 nach A2 wird zuerst und allein gedrängt.
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen);
        optimierer.Rechnen(80_000);
        optimierer.EinteilungAendern(Stufeneinteilung.Standard.Platzieren(Kostenkriterium.Auswaertskoppel, Kostenkriterium.Hallenbelegung, davor: true));
        Lenkungsstand geaendert = Assert.IsType<Lenkungsstand>(optimierer.Lenkung);
        Assert.NotNull(geaendert.Basis);
        Assert.Equal("Optimierung Stufe A", geaendert.Bezeichnung);
        Assert.Equal([Kostenkriterium.Auswaertskoppel], geaendert.Kriterien);
        Assert.EndsWith("ab Auswaertskoppel", geaendert.Aenderungen[^1], StringComparison.Ordinal);

        // Festlegung (gewählte Alternative): Jeder Verstoß über einer Obergrenze zählt wie ein harter Fehler; aufgehoben wieder nicht.
        using var fest = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen);
        fest.Rechnen(80_000);
        Stufenwert vorher = Assert.IsType<Lenkungsstand>(fest.Lenkung).Bester;
        Stufeneinteilung voll = Stufeneinteilung.Standard.Vervollstaendigt();
        fest.Festlegen(voll.A.Concat(voll.B).ToDictionary(k => k, _ => 0), "Test");
        Lenkungsstand festgelegt = Assert.IsType<Lenkungsstand>(fest.Lenkung);
        Assert.Equal("Test", festgelegt.Festgelegt);
        Assert.Equal(vorher.Harte + vorher.A + vorher.B, festgelegt.Bester.Harte);
        fest.Festlegen(null, null);
        Lenkungsstand aufgehoben = Assert.IsType<Lenkungsstand>(fest.Lenkung);
        Assert.Null(aufgehoben.Festgelegt);
        Assert.Equal(vorher.Harte, aufgehoben.Bester.Harte);

        // Zählt die Suche beim Glätten schon und rückt ein Kriterium von C nach B, zählt sie gleich nach der neuen Einteilung
        // weiter – kein Drängen mit den Kosten des Originals, bei dem die Inseln Verstöße in A gegen B tauschten.
        fest.Festlegen(voll.A.Concat(voll.B).ToDictionary(k => k, _ => 9999), "Test");
        fest.Rechnen(20_000);
        Assert.Equal("Optimierung Stufe C", fest.Lenkung?.Bezeichnung);
        Kostenkriterium nachB = voll.C.First(Stufeneinteilung.HatAnzahl);
        fest.EinteilungAendern(voll.Einstufen(nachB, Stufe.B));
        Lenkungsstand gezaehltNeu = Assert.IsType<Lenkungsstand>(fest.Lenkung);
        Assert.Equal("Optimierung Stufe C", gezaehltNeu.Bezeichnung);
        Assert.Contains("Einteilung geändert", gezaehltNeu.Aenderungen[^1], StringComparison.Ordinal);
        fest.Rechnen(2_000);
        Assert.Equal("Optimierung Stufe C", fest.Lenkung?.Bezeichnung);

        // Feststeckende Inseln beginnen reihum mit einem anderen Startverfahren neu (ohne Teilneuplanung statt ihres Plans
        // „Mannschaften“).
        fest.Rechnen(40_000);
        IReadOnlyList<Startstatistik> starts = fest.Maschinenraum.Starts ?? [];
        Assert.True(starts.Count >= 2, string.Join(", ", starts));
        Assert.Contains(starts, x => x.Verfahren == Startverfahren.Spiele);
        Assert.Contains(starts, x => x.Verfahren == Startverfahren.Zeitfenster);
        Assert.All(starts, x => Assert.True(x.Starts > 0));
        Assert.Contains(fest.Maschinenraum.Inseln, i => i.Start is not null);

        // Wechsel von der Kostenoptimierung: Der Plan ist schon optimiert, also keine Basisoptimierung, gleich Stufe A.
        using var direkt = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen with { AutoOhneGrundlauf = true });
        direkt.Rechnen(20_000);
        Lenkungsstand ohneGrundlauf = Assert.IsType<Lenkungsstand>(direkt.Lenkung);
        Assert.NotNull(ohneGrundlauf.Basis);
        Assert.NotEqual("Basisoptimierung", ohneGrundlauf.Bezeichnung);
    }

    [Fact]
    public void Basisoptimierung_endet_trotz_kleiner_Verbesserungen()
    {
        // Alle 9 s ein paar Kosten weniger (unter 1 %): Das hielt die Basisoptimierung früher beliebig lange fest.
        Automodus auto = NeuerAutomodus();
        Assert.False(Schritt(auto, 0, 0, 1000));
        Assert.False(Schritt(auto, 9, 9_000_000, 999));
        Assert.True(Schritt(auto, 18, 18_000_000, 998));
    }

    [Fact]
    public void Deutliche_Verbesserung_verlaengert_die_Basisoptimierung()
    {
        Automodus auto = NeuerAutomodus();
        Assert.False(Schritt(auto, 0, 0, 1000));
        Assert.False(Schritt(auto, 9, 9_000_000, 950));
        Assert.False(Schritt(auto, 18, 18_000_000, 949));
        Assert.True(Schritt(auto, 19.5, 19_500_000, 949));
    }

    [Fact]
    public void Auf_einem_langsamen_Rechner_zaehlen_die_berechneten_Plaene()
    {
        // 10.000 Pläne/s: Nach 20 s sind erst 200.000 Pläne berechnet, der Stillstand braucht 500.000.
        Automodus auto = NeuerAutomodus();
        Assert.False(Schritt(auto, 0, 0, 1000));
        Assert.False(Schritt(auto, 20, 200_000, 1000));
        Assert.True(Schritt(auto, 50, 500_000, 1000));
    }

    [Fact]
    public void Basisoptimierung_endet_spaetestens_nach_der_Hoechstdauer()
    {
        // Alle 5 s eine deutliche Verbesserung: Die Suche steht nie, nach 180 s geht es trotzdem mit Stufe A weiter.
        Automodus auto = NeuerAutomodus();
        double kosten = 1000;
        for (int sekunden = 0; sekunden < 180; sekunden += 5)
        {
            Assert.False(Schritt(auto, sekunden, sekunden * 1_000_000L, kosten));
            kosten *= 0.95;
        }

        Assert.True(Schritt(auto, 180, 180_000_000, kosten));
    }

    [Fact]
    public void Stufenwert_ordnet_nach_harten_Fehlern_A_B_C_dann_Ausreissern_und_erst_dann_nach_Kosten()
    {
        Assert.True(new Stufenwert(0, 9, 9, 9, 9, 9e9).IstBesserAls(new Stufenwert(1, 0, 0, 0, 0, 0)));
        Assert.True(new Stufenwert(0, 1, 9, 9, 9, 9e9).IstBesserAls(new Stufenwert(0, 2, 0, 0, 0, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 9, 9, 9e9).IstBesserAls(new Stufenwert(0, 1, 2, 0, 0, 0)));

        // Weniger C-Verstöße schlagen weniger Ausreißer: Ein besserer Plan hat bei gleichem A und B nie mehr C-Verstöße.
        Assert.True(new Stufenwert(0, 1, 1, 9, 1, 9e9).IstBesserAls(new Stufenwert(0, 1, 1, 0, 2, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 1, 1, 9e9).IstBesserAls(new Stufenwert(0, 1, 1, 2, 1, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 1, 1, 5).IstBesserAls(new Stufenwert(0, 1, 1, 1, 1, 6)));
        Assert.False(new Stufenwert(0, 1, 1, 1, 1, 6).IstBesserAls(new Stufenwert(0, 1, 1, 1, 1, 6)));
    }

    [Fact]
    public void Stufenvergleich_gibt_B_nie_fuer_C_her()
    {
        // Ein B-Verstoß mehr ist schlechter, egal wie viel besser C wird; A geht vor B.
        Assert.False(Stufenvergleich.Besser(new Stufenwert(0, 1, 2, 0, 0, 0), new Stufenwert(0, 1, 1, 0, 99, 0)));
        Assert.False(Stufenvergleich.Besser(new Stufenwert(0, 2, 0, 0, 0, 0), new Stufenwert(0, 1, 9, 0, 99, 0)));

        // Zurückziehen beim Glätten: mit einem B-Verstoß Spielraum, A nie.
        var bezug = new Stufenwert(0, 1, 1, 0, 20, 0);
        Assert.False(Stufenvergleich.AOderBSchlechter(new Stufenwert(0, 1, 2, 0, 30, 0), bezug));
        Assert.True(Stufenvergleich.AOderBSchlechter(new Stufenwert(0, 1, 3, 0, 0, 0), bezug));
        Assert.True(Stufenvergleich.AOderBSchlechter(new Stufenwert(0, 2, 0, 0, 0, 0), bezug));
    }

    [Fact]
    public void Stufeneinteilung_liest_die_Angaben_des_Staffelleiters_mit_der_Reihenfolge_von_C()
    {
        Stufeneinteilung? einteilung = Stufeneinteilung.Lesen("A:Hallenbelegung, Pflichtspieltage; B:ParalleleSpiele; C:Spielverteilung,DreiTageAbstand");
        Assert.NotNull(einteilung);
        Stufe[] stufen = einteilung.AlsFeld();

        Assert.Equal(Stufe.A, stufen[(int)Kostenkriterium.Hallenbelegung]);
        Assert.Equal(Stufe.A, stufen[(int)Kostenkriterium.Pflichtspieltage]);
        Assert.Equal(Stufe.B, stufen[(int)Kostenkriterium.ParalleleSpiele]);
        Assert.Equal(Stufe.C, stufen[(int)Kostenkriterium.Sperrtermine]);
        Kostenkriterium[] zuerst = [Kostenkriterium.Spielverteilung, Kostenkriterium.DreiTageAbstand];
        Assert.Equal(zuerst, einteilung.C.Take(2));
        Assert.Equal(Stufeneinteilung.AnzahlKriterien - 3, einteilung.C.Count);
        Assert.Contains(Kostenkriterium.Sperrtermine, einteilung.C);
        Assert.Null(Stufeneinteilung.Lesen("A:Gibtsnicht"));
        Assert.Null(Stufeneinteilung.Lesen("X:Hallenbelegung"));
        Assert.Null(Stufeneinteilung.Lesen("A:Spieltaglaenge"));
        Assert.Null(Stufeneinteilung.Lesen("A:Hallenbelegung;B:Hallenbelegung"));
        Assert.Equal(Stufe.B, Stufeneinteilung.Standard.AlsFeld()[(int)Kostenkriterium.Sperrtermine]);
    }

    [Fact]
    public void Schlechteste_Insel_wird_neu_gestartet()
    {
        Staffel staffel = LeereStaffel(R2);
        using var optimierer = new Inseloptimierer(
            staffel,
            Berechnungsoptionen.Standard,
            Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 3, DurchlaeufeVorNeustart = 2_000 });

        optimierer.Rechnen(150_000);

        Assert.True(optimierer.Neustarts > 0);
        PlanPruefen(staffel, optimierer.BesterStand());
    }

    [Fact]
    public void Spezial_Insel_startet_und_ihre_Loesung_startet_Inseln_neu()
    {
        Staffel staffel = LeereStaffel(R2);
        using var optimierer = new Inseloptimierer(
            staffel,
            Berechnungsoptionen.Standard,
            Optimierungseinstellungen.Standard with
            {
                Kerne = 1,
                Startwert = 5,
                DurchlaeufeVorNeustart = 2_000,
                DurchlaeufeVorSpezialInsel = 5_000,
                MindestDurchlaeufeSpezialInsel = 3_000,
            });

        // In Schritten rechnen und aufhören, sobald die Spezial-Insel eine Insel neu gestartet hat (statt immer 200 000).
        for (long durchlaeufe = 10_000; optimierer.SpezialVerwendet == 0 && durchlaeufe <= 200_000; durchlaeufe += 10_000)
        {
            optimierer.Rechnen(durchlaeufe);
        }

        Assert.NotNull(optimierer.SpezialKostenart);
        Assert.True(optimierer.SpezialVerwendet > 0);
        PlanPruefen(staffel, optimierer.BesterStand());
    }

    [Fact]
    public async Task Parallelbetrieb_liefert_einen_gueltigen_Plan()
    {
        Staffel staffel = LeereStaffel(R2);
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 2 });

        optimierer.Starten();
        await Task.Delay(1500);
        optimierer.Stoppen();

        Assert.Null(optimierer.Fehler);
        Assert.True(optimierer.Durchlaeufe > 0);
        PlanPruefen(staffel, optimierer.BesterStand());
    }

    /// <summary>Vorrang für HiGHS: Ruhen mehr Threads als es gibt, rechnet einer trotzdem weiter.</summary>
    /// <returns>Der Testlauf.</returns>
    [Fact]
    public async Task Ruhende_Threads_lassen_einen_rechnen()
    {
        using var optimierer = new Inseloptimierer(LeereStaffel(R2), Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 2 });
        optimierer.Ruhend = 8;
        Assert.Equal(1, optimierer.Ruhend);
        optimierer.Starten();
        long anfang = optimierer.Durchlaeufe;
        for (int i = 0; i < 50 && optimierer.Durchlaeufe == anfang; i++)
        {
            await Task.Delay(100);
        }

        Assert.True(optimierer.Durchlaeufe > anfang);
        optimierer.Ruhend = 0;
        Assert.Equal(0, optimierer.Ruhend);

        // Mit acht Kernen ruhen höchstens vier (die Hälfte).
        using var achtKerne = new Inseloptimierer(LeereStaffel(R2), Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 8 });
        achtKerne.Ruhend = 8;
        Assert.Equal(4, achtKerne.Ruhend);
    }

    /// <summary>
    /// Ein Platz, der ruhte, bekommt den neuen Plan (andere Gewichte, Zählziel) auch dann, wenn vor seiner Übernahme ein
    /// Neustart ohne neuen Plan kam – sonst rechnete er mit den alten Gewichten weiter.
    /// </summary>
    [Fact]
    public void Neustart_ersetzt_den_neuen_Plan_eines_Auftrags_nicht()
    {
        RefPlan referenz = RefPlan.Laden(LeereStaffel(R2), Berechnungsoptionen.Standard);
        var definition = new KernDefinition(referenz);
        var alt = new KernPlan(referenz, definition);
        alt.Merken();
        var platz = new Suchplatz(alt, new Zufall(1), Tauschstrategie.Lesen("2"), false, -1);
        var neu = new KernPlan(referenz, definition) { Zaehlziel = new Zaehlziel([.. Enumerable.Repeat(Stufe.C, 64)], 1000) };
        neu.Merken();
        var start = new Loesung(alt.AnzahlErlaubt);
        alt.LoesungSchreiben(start);
        platz.Auftragen(new Auftrag(start, -1, neu));
        platz.Auftragen(new Auftrag(start.Kopie(), -1));
        platz.Rechnen();
        Assert.True(platz.Spaet);
    }

    [Fact]
    public async Task Pause_haelt_die_Rechnung_an()
    {
        using var optimierer = new Inseloptimierer(LeereStaffel(R2), Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 2 });
        optimierer.Starten();
        await Task.Delay(300);

        // Unter Last (andere Tests rechnen parallel) braucht das Anhalten bzw. Weiterlaufen etwas länger: warten, bis sich
        // der Zähler nicht mehr bzw. wieder bewegt, höchstens fünf Sekunden.
        optimierer.Pausiert = true;
        long angehalten = await StehtAsync(() => optimierer.Durchlaeufe);
        await Task.Delay(300);
        Assert.Equal(angehalten, optimierer.Durchlaeufe);

        optimierer.Pausiert = false;
        for (int i = 0; i < 50 && optimierer.Durchlaeufe == angehalten; i++)
        {
            await Task.Delay(100);
        }

        Assert.True(optimierer.Durchlaeufe > angehalten);
    }

    /// <summary>Ein Startplan (O7 Schritt 2, z. B. aus HiGHS) ist sofort der beste Stand, wenn er besser ist als der leere Plan.</summary>
    [Fact]
    public void Startplan_ist_der_Ausgangspunkt()
    {
        const string R3 = "R3_Damen_Bezirksoberliga_Rheinhessen (1).xml";
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", R3);
        IReadOnlyList<Spiel> startplan = StaffelAbbildung.AusPlanDaten(ClickTtNachPlanDaten.Laden(pfad)).BestehenderSpielplan;
        Staffel staffel = LeereStaffel(R3);
        Assert.NotEmpty(startplan);

        using var ohne = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 1 });
        using var mit = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 1, Startwert = 1, Startplan = startplan });

        Optimierungsergebnis start = mit.BesterStand();
        Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = startplan }, Berechnungsoptionen.Standard);
        Assert.Equal(Kostenanzeige.Kurz(bewertung.Gesamtkosten), Kostenanzeige.Kurz(start.Kosten));
        Assert.True(start.Kosten < ohne.BesterStand().Kosten);
        Assert.Equal(startplan.Count, start.Spiele.Count(s => s.Zeitpunkt is not null));

        mit.Rechnen(20_000);
        Assert.True(mit.BesterStand().Kosten <= start.Kosten);
        PlanPruefen(staffel, mit.BesterStand());
    }

    /// <summary>
    /// Automodus mit HiGHS: Der Plan aus HiGHS beendet den Grundlauf und wird gleich bester Plan, und die Schranke beendet das Drängen sofort, wenn der beste
    /// Plan sie erreicht (hier eine Schranke, die jeder Plan ohne harte Fehler erreicht).
    /// </summary>
    [Fact]
    public void Schranke_aus_HiGHS_beendet_das_Draengen_und_der_Plan_wird_eingespielt()
    {
        const string R3 = "R3_Damen_Bezirksoberliga_Rheinhessen (1).xml";
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", R3);
        IReadOnlyList<Spiel> plan = StaffelAbbildung.AusPlanDaten(ClickTtNachPlanDaten.Laden(pfad)).BestehenderSpielplan;
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with
        {
            Kerne = 1,
            Startwert = 3,
            Automodus = true,
            AutoOhneGrundlauf = true,
            AutoStillstandPlaene = 2_000,
            Zeitbudget = 2_000,
        };
        using var optimierer = new Inseloptimierer(LeereStaffel(R3), Berechnungsoptionen.Standard, einstellungen);
        optimierer.HighsEinspielen(plan, 1000, 1000);
        optimierer.Rechnen(20_000);

        Lenkungsstand lenkung = Assert.IsType<Lenkungsstand>(optimierer.Lenkung);
        Assert.Contains(lenkung.Aenderungen, z => z.EndsWith("Basisoptimierung beendet", StringComparison.Ordinal));
        Assert.Contains(lenkung.Aenderungen, z => z.Contains("Plan aus HiGHS (", StringComparison.Ordinal) && z.EndsWith("bester Plan", StringComparison.Ordinal));
        Assert.Contains(lenkung.Aenderungen, z => z.Contains("Stufe A bei der Schranke", StringComparison.Ordinal));
        Assert.Contains(lenkung.Aenderungen, z => z.Contains("Stufe B bei der Schranke", StringComparison.Ordinal));
        Assert.Equal((1000, 1000), (lenkung.SchrankeA, lenkung.SchrankeB));
        Assert.Equal(0, lenkung.Bester.Harte);
    }

    /// <summary>
    /// Ohne Schranke aus HiGHS: Geht bei A nichts mehr, drängt der Automodus B nicht mit angehobenen Gewichten (damit liefen die
    /// Inseln vom besten Plan weg, bis zu Spielen ohne Termin), sondern schützt A und sucht zählend weiter.
    /// </summary>
    [Fact]
    public void Ohne_Schranke_sucht_der_Automodus_nach_Stufe_A_zaehlend()
    {
        const string R3 = "R3_Damen_Bezirksoberliga_Rheinhessen (1).xml";
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with
        {
            Kerne = 1,
            Startwert = 3,
            Automodus = true,
            AutoOhneGrundlauf = true,
            AutoStillstandPlaene = 2_000,
            Zeitbudget = 2_000,
        };
        using var optimierer = new Inseloptimierer(LeereStaffel(R3), Berechnungsoptionen.Standard, einstellungen);
        optimierer.Rechnen(300_000);

        Lenkungsstand lenkung = Assert.IsType<Lenkungsstand>(optimierer.Lenkung);
        Assert.Contains(lenkung.Aenderungen, z => z.Contains("bei A geht nichts mehr", StringComparison.Ordinal) && z.EndsWith("A geschützt, Suche zählt B und C", StringComparison.Ordinal));
        Assert.DoesNotContain(lenkung.Aenderungen, z => z.EndsWith("): Stufe B", StringComparison.Ordinal));
        Assert.Equal(0, lenkung.Bester.Harte);
    }

    /// <summary>
    /// Teilneuplanung im Automodus: Pläne werden wie der beste Plan bewertet (vor dem Ende der Basisoptimierung gar nicht) und
    /// nur übernommen, wenn sie danach besser sind.
    /// </summary>
    [Fact]
    public void Plan_der_Teilneuplanung_wird_wie_der_beste_Plan_bewertet()
    {
        const string R3 = "R3_Damen_Bezirksoberliga_Rheinhessen (1).xml";
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", R3);
        IReadOnlyList<Spiel> plan = StaffelAbbildung.AusPlanDaten(ClickTtNachPlanDaten.Laden(pfad)).BestehenderSpielplan;
        Optimierungseinstellungen einstellungen = Optimierungseinstellungen.Standard with
        {
            Kerne = 1,
            Startwert = 3,
            Automodus = true,
            AutoOhneGrundlauf = true,
            AutoStillstandPlaene = 2_000,
            Zeitbudget = 2_000,
        };
        using var optimierer = new Inseloptimierer(LeereStaffel(R3), Berechnungsoptionen.Standard, einstellungen);
        Assert.Null(optimierer.NachStufen(plan));
        Assert.False(optimierer.PlanEinspielen(plan, "Test"));

        optimierer.Rechnen(5_000);
        Stufenwert wert = Assert.IsType<Stufenwert>(optimierer.NachStufen(plan));
        Stufenwert bester = optimierer.Lenkung!.Bester;
        bool besser = optimierer.IstBesser(wert, bester);
        Assert.Equal(besser, optimierer.PlanEinspielen(plan, "Test"));
        Assert.Contains(optimierer.Lenkung.Aenderungen, z => z.Contains("Test (", StringComparison.Ordinal));
    }

    /// <summary>Wartet, bis sich ein Zähler 300 ms lang nicht mehr ändert (höchstens fünf Sekunden), und liefert seinen Stand.</summary>
    private static async Task<long> StehtAsync(Func<long> zaehler)
    {
        long stand = zaehler();
        for (int i = 0; i < 16; i++)
        {
            await Task.Delay(300);
            long neu = zaehler();
            if (neu == stand)
            {
                break;
            }

            stand = neu;
        }

        return stand;
    }

    private static Optimierungsergebnis Rechnen(Staffel staffel, Optimierungseinstellungen einstellungen, long durchlaeufe)
    {
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen);
        optimierer.Rechnen(durchlaeufe);
        return optimierer.BesterStand();
    }

    private static Automodus NeuerAutomodus()
    {
        RefPlan referenz = RefPlan.Laden(LeereStaffel(R2), Berechnungsoptionen.Standard);
        return new Automodus(referenz, new KernDefinition(referenz), Optimierungseinstellungen.Standard with { Automodus = true });
    }

    /// <summary>Ein Schritt der Basisoptimierung.</summary>
    /// <param name="auto">Der Automodus.</param>
    /// <param name="sekunden">Sekunden reiner Rechenzeit.</param>
    /// <param name="plaene">Berechnete Pläne seit dem Start.</param>
    /// <param name="kosten">Beste Kosten der Suche.</param>
    /// <returns><c>true</c>, wenn die Basisoptimierung vorbei ist.</returns>
    private static bool Schritt(Automodus auto, double sekunden, long plaene, double kosten)
    {
        auto.Zaehlen(plaene);
        auto.Beobachten(kosten, sekunden);
        return auto.GrundlaufVorbei(sekunden);
    }

    private static (Optimierungsergebnis Ergebnis, Lenkungsstand? Lenkung) MitLenkung(Staffel staffel, Optimierungseinstellungen einstellungen, long durchlaeufe)
    {
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen);
        optimierer.Rechnen(durchlaeufe);
        return (optimierer.BesterStand(), optimierer.Lenkung);
    }

    /// <summary>Der gefundene Plan, als bestehender Spielplan bewertet, ist gültig und kostet dasselbe wie im Optimierer.</summary>
    private static void PlanPruefen(Staffel staffel, Optimierungsergebnis ergebnis)
    {
        Planbewertung bewertung = Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = ergebnis.Spiele }, Berechnungsoptionen.Standard);
        Assert.Equal(0, bewertung.UngueltigeSpiele);
        Assert.Equal(0, bewertung.SpieleAnSpielfreienTagen);
        Assert.Equal(Kostenanzeige.Kurz(ergebnis.Kosten), Kostenanzeige.Kurz(bewertung.Gesamtkosten));
    }

    private static Staffel LeereStaffel(string datei)
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", datei);
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        string modifikationen = Path.ChangeExtension(pfad, ".modifications");
        if (File.Exists(modifikationen))
        {
            stand.Zusammenfuehren(PlanDatenDatei.Laden(modifikationen));
        }

        return StaffelAbbildung.AusPlanDaten(stand) with { BestehenderSpielplan = [] };
    }
}
