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

        // Wechsel von der Kostenoptimierung: Der Plan ist schon optimiert, also keine Basisoptimierung, gleich Stufe A.
        using var direkt = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen with { AutoOhneGrundlauf = true });
        direkt.Rechnen(20_000);
        Lenkungsstand ohneGrundlauf = Assert.IsType<Lenkungsstand>(direkt.Lenkung);
        Assert.NotNull(ohneGrundlauf.Basis);
        Assert.NotEqual("Basisoptimierung", ohneGrundlauf.Bezeichnung);
    }

    [Fact]
    public void Stufenwert_ordnet_nach_harten_Fehlern_A_B_Ausreissern_C_und_erst_dann_nach_Kosten()
    {
        Assert.True(new Stufenwert(0, 9, 9, 9, 9, 9e9).IstBesserAls(new Stufenwert(1, 0, 0, 0, 0, 0)));
        Assert.True(new Stufenwert(0, 1, 9, 9, 9, 9e9).IstBesserAls(new Stufenwert(0, 2, 0, 0, 0, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 9, 9, 9e9).IstBesserAls(new Stufenwert(0, 1, 2, 0, 0, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 1, 9, 9e9).IstBesserAls(new Stufenwert(0, 1, 1, 2, 0, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 1, 1, 9e9).IstBesserAls(new Stufenwert(0, 1, 1, 1, 2, 0)));
        Assert.True(new Stufenwert(0, 1, 1, 1, 1, 5).IstBesserAls(new Stufenwert(0, 1, 1, 1, 1, 6)));
        Assert.False(new Stufenwert(0, 1, 1, 1, 1, 6).IstBesserAls(new Stufenwert(0, 1, 1, 1, 1, 6)));
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

    [Fact]
    public async Task Pause_haelt_die_Rechnung_an()
    {
        using var optimierer = new Inseloptimierer(LeereStaffel(R2), Berechnungsoptionen.Standard, Optimierungseinstellungen.Standard with { Kerne = 2 });
        optimierer.Starten();
        await Task.Delay(300);

        optimierer.Pausiert = true;
        await Task.Delay(300);
        long angehalten = optimierer.Durchlaeufe;
        await Task.Delay(300);
        Assert.Equal(angehalten, optimierer.Durchlaeufe);

        optimierer.Pausiert = false;
        await Task.Delay(300);
        Assert.True(optimierer.Durchlaeufe > angehalten);
    }

    private static Optimierungsergebnis Rechnen(Staffel staffel, Optimierungseinstellungen einstellungen, long durchlaeufe)
    {
        using var optimierer = new Inseloptimierer(staffel, Berechnungsoptionen.Standard, einstellungen);
        optimierer.Rechnen(durchlaeufe);
        return optimierer.BesterStand();
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
