// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Der einfache Modus: fester Maßstab (Pflicht, Wünsche, Güte), schnelle Schranke und das Rennen der Einteilungen.</summary>
[Collection(MessungenSammlung.Name)]
public sealed class SchnellplanerTests
{
    [Fact]
    public void Die_Grundeinteilung_hat_in_B_nur_die_Wuensche()
    {
        Stufeneinteilung grund = Schnellplaner.Grundeinteilung();

        Assert.Equal(Planguete.Pflicht, grund.A);
        Assert.Equal(Planguete.Wunschkriterien, grund.B);
        Assert.Equal(Stufeneinteilung.Standard.C, grund.C);
        Assert.Equal(Stufeneinteilung.AnzahlKriterien, grund.A.Count + grund.B.Count + grund.C.Count);
    }

    [Fact]
    public void Der_Massstab_zaehlt_Pflicht_vor_Wuenschen_vor_Fokus_vor_Guete()
    {
        int[] wenig = new int[Stufeneinteilung.AnzahlKriterien];
        int[] mehrAbstand = [.. wenig];
        mehrAbstand[(int)Kostenkriterium.DreiTageAbstand] = 20;
        int[] mehrWechsel = [.. wenig];
        mehrWechsel[(int)Kostenkriterium.WechselHeimAuswaerts] = 15;
        var gut = new Planguete(0, 0, 1, 0, wenig, 10);
        Massstab m = Massstab.Standard;

        Assert.True(m.IstBesser(gut with { Anzahl = mehrAbstand }, gut with { A = 1 }));
        Assert.True(m.IstBesser(gut with { Anzahl = mehrAbstand }, gut with { Wuensche = 2 }));

        // Ohne Fokus zählt die Güte: Heim/Auswärts wiegt 3 (geteilt durch drei Kriterien), Abstände 2 (durch zwei).
        Assert.Equal(3 * 15 / 3.0 / 10, m.Guete(gut with { Anzahl = mehrWechsel }), 9);
        Assert.Equal(2 * 20 / 2.0 / 10, m.Guete(gut with { Anzahl = mehrAbstand }), 9);
        Assert.True(m.IstBesser(gut with { Anzahl = mehrWechsel }, gut with { Anzahl = mehrAbstand }));

        // Mit Fokus auf die Abstände gewinnt der Plan mit weniger Abständen, auch wenn er sonst schlechter ist.
        Massstab fokus = m with { Fokus = 1 };
        int[] beides = [.. mehrWechsel];
        beides[(int)Kostenkriterium.WechselHeimAuswaerts] = 80;
        Assert.True(fokus.IstBesser(gut with { Anzahl = beides }, gut with { Anzahl = mehrAbstand }));
        Assert.False(m.IstBesser(gut with { Anzahl = beides }, gut with { Anzahl = mehrAbstand }));

        // Bezug: Jedes Kriterium zählt relativ zu seinem Wert im Grundplan (mindestens die Zahl der Mannschaften).
        int[] bezug = [.. wenig];
        bezug[(int)Kostenkriterium.WechselHeimAuswaerts] = 40;
        Assert.Equal(15 / 40.0 / 3, (m with { Bezug = bezug }).Gruppe(gut with { Anzahl = mehrWechsel }, 0), 9);
    }

    [Fact]
    public void Die_Grenze_behaelt_nur_nicht_dominierte_Plaene_und_zeigt_was_eine_Senkung_kostet()
    {
        var front = new Paretofront();
        Massstab m = Massstab.Standard;
        static Planguete Plan(int wechsel, int abstand, int wuensche = 0)
        {
            int[] je = new int[Stufeneinteilung.AnzahlKriterien];
            je[(int)Kostenkriterium.WechselHeimAuswaerts] = wechsel;
            je[(int)Kostenkriterium.DreiTageAbstand] = abstand;
            return new Planguete(0, 0, wuensche, 0, je, 10);
        }

        Assert.True(front.Aufnehmen([], Plan(20, 10), m));
        Assert.True(front.Aufnehmen([], Plan(10, 20), m));
        Assert.False(front.Aufnehmen([], Plan(20, 20), m));
        Assert.True(front.Aufnehmen([], Plan(15, 10), m));
        Assert.Equal(2, front.Plaene.Count);

        // Mehr Wünsche verletzt: nie an der Grenze.
        Assert.False(front.Aufnehmen([], Plan(0, 0, wuensche: 1), m));

        // Wippe: Heim/Auswärts senken (vom Plan 15/10 aus) kostet Abstände; Abstände senken geht nicht – an der Grenze.
        double[] aktuell = [.. Enumerable.Range(0, Massstab.Gruppen.Count).Select(g => m.Gruppe(Plan(15, 10), g))];
        (Grenzplan senkung, IReadOnlyList<double> aenderung) = Assert.IsType<(Grenzplan, IReadOnlyList<double>)>(front.Senken(aktuell, 0, m));
        Assert.Equal(10, senkung.Guete.Anzahl[(int)Kostenkriterium.WechselHeimAuswaerts]);
        Assert.True(aenderung[0] < 0 && aenderung[1] > 0);
        Assert.Null(front.Senken(aktuell, 1, m));
    }

    [HighsFact]
    public void Die_schnelle_Schranke_rechnet_nur_A_und_B_und_gibt_keine_Varianten_heraus()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml");

        Schrankenbericht bericht = Ligaschranke.Schnell(staffel, Berechnungsoptionen.Standard, new Highsbruecke(), Schnellplaner.Grundeinteilung(), TimeSpan.Zero);

        Assert.NotNull(bericht.SchrankeA);
        Assert.NotNull(bericht.SummeB);
        Assert.Empty(bericht.Proben!);
        Assert.Empty(bericht.Alternativen!);
        Assert.DoesNotContain(bericht.Stufen, s => s.Stufe[0] == 'C');
    }

    [Fact]
    public void Die_Vorschlaege_unterscheiden_sich_wesentlich_und_nennen_den_Unterschied()
    {
        Massstab m = Massstab.Standard;
        static Grenzplan Plan(int wechsel, int abstand, Massstab m)
        {
            int[] je = new int[Stufeneinteilung.AnzahlKriterien];
            je[(int)Kostenkriterium.WechselHeimAuswaerts] = wechsel;
            je[(int)Kostenkriterium.DreiTageAbstand] = abstand;
            var guete = new Planguete(0, 0, 0, 0, je, 10);
            return new Grenzplan([], guete, [.. Enumerable.Range(0, Massstab.Gruppen.Count).Select(g => m.Gruppe(guete, g))]);
        }

        // Ausgewogen 20/10 (Güte 2,0 + 1,0), bester Wechsel 4/30, beste Abstände 40/2, fast gleich wie die Empfehlung 19/12.
        Grenzplan ausgewogen = Plan(20, 10, m);
        Grenzplan wechsel = Plan(4, 30, m);
        Grenzplan abstand = Plan(40, 2, m);
        Grenzplan fastGleich = Plan(19, 12, m);
        IReadOnlyList<Vorschlag> v = Vorschlagsauswahl.Waehlen([fastGleich, wechsel, ausgewogen, abstand], m, 0.4);

        Assert.Equal(3, v.Count);
        Assert.Same(ausgewogen, v[0].Plan);
        Assert.Empty(v[0].Unterschiede);
        Vorschlag h = Assert.Single(v, x => x.Gruppe == 0);
        Assert.Same(wechsel, h.Plan);
        Assert.Contains(new Unterschied(Kostenkriterium.WechselHeimAuswaerts, 20, 4), h.Unterschiede);
        Assert.Contains(new Unterschied(Kostenkriterium.DreiTageAbstand, 10, 30), h.Unterschiede);
        Assert.Same(abstand, Assert.Single(v, x => x.Gruppe == 1).Plan);

        // Höhere Stufe, kleinere Schwelle: Auch der fast gleiche Plan ist ein Vorschlag, dazu die bisherige Wahl zum Vergleich.
        IReadOnlyList<Vorschlag> spaeter = Vorschlagsauswahl.Waehlen([ausgewogen, fastGleich], m, 0.05, Plan(22, 9, m));
        Assert.Contains(spaeter, x => ReferenceEquals(x.Plan, fastGleich));
        Assert.Equal("Bisherige Wahl", spaeter[^1].Name);

        // Mit Hebel ist die Empfehlung der beste der Hebelgruppe, die ausgewogene Wahl bleibt daneben.
        IReadOnlyList<Vorschlag> hebel = Vorschlagsauswahl.Waehlen([ausgewogen, wechsel, abstand], m with { Fokus = 1 }, 0.4);
        Assert.Same(abstand, hebel[0].Plan);
        Assert.StartsWith("Empfehlung: ", hebel[0].Name, StringComparison.Ordinal);
        Assert.Contains(hebel, x => x.Name == "Ausgewogen" && ReferenceEquals(x.Plan, ausgewogen));
    }

    [Fact]
    public void Der_Beweisstand_trennt_optimal_bewiesen_allein_bewiesen_und_offen()
    {
        int[] je = new int[Stufeneinteilung.AnzahlKriterien];
        je[(int)Kostenkriterium.ParalleleSpiele] = 2;
        je[(int)Kostenkriterium.Sperrtermine] = 3;
        je[(int)Kostenkriterium.Spielverteilung] = 5;
        je[(int)Kostenkriterium.WechselHeimAuswaerts] = 9;
        var guete = new Planguete(0, 2, 3, 0, je, 10);
        var bericht = new Schrankenbericht(
            0,
            0,
            [
                new Schrankenstufe("A1", "Spiele ohne Termin", 0, 0, true, 0),
                new Schrankenstufe("A3", "Parallele Spiele", 2, 2, true, 0, Kostenkriterium.ParalleleSpiele, 2),
                new Schrankenstufe("B3", "Sperrtermine", 1, 3, false, 0, Kostenkriterium.Sperrtermine, 1),
            ],
            [],
            [],
            SummeA: new Schrankenstufe("A", "Summe A", 2, 2, true, 0),
            SummeB: new Schrankenstufe("B", "Summe B", 2, 3, false, 0),
            Proben: [new Kriteriumsprobe(Kostenkriterium.Spielverteilung, 5, 5, 5, [])]);

        IReadOnlyList<Kriteriumsstatus> status = Kriteriumsstatus.Bestimmen(guete, bericht);
        Beweisstand Stand(Kostenkriterium k) => status.Single(s => s.Kriterium == k).Stand;

        Assert.Equal(Beweisstand.Optimal, Stand(Kostenkriterium.Hallenbelegung));
        Assert.Equal(Beweisstand.Bewiesen, Stand(Kostenkriterium.ParalleleSpiele));
        Assert.Equal(Beweisstand.Offen, Stand(Kostenkriterium.Sperrtermine));
        Assert.Equal(1, status.Single(s => s.Kriterium == Kostenkriterium.Sperrtermine).Untergrenze);
        Assert.Equal(Beweisstand.AlleinBewiesen, Stand(Kostenkriterium.Spielverteilung));
        Assert.Equal(Beweisstand.Offen, Stand(Kostenkriterium.WechselHeimAuswaerts));
        Assert.Equal(Beweisstand.Optimal, Stand(Kostenkriterium.Setzliste));
    }

    [HighsFact]
    public void Der_einfache_Modus_liefert_einen_gueltigen_Plan_an_der_Grenze_und_folgt_Wahl_und_Hebel()
    {
        Staffel staffel = Laden("R3_Damen_Bezirksoberliga_Rheinhessen (1).xml");
        Schnellplanereinstellungen e = Schnellplanereinstellungen.Standard with
        {
            Kerne = 1,
            Vorglaetten = TimeSpan.FromSeconds(3),
            ErsteRunde = TimeSpan.FromSeconds(3),
            Startwert = 5,
        };
        var steuerung = new Schnellsteuerung();
        var staende = new List<Schnellstand>();
        Grenzplan? gewaehlt = null;
        using var abbruch = new CancellationTokenSource(TimeSpan.FromSeconds(45));

        // Mitbestimmen: Sobald es Vorschläge gibt, wählt der Klassenleiter die Empfehlung mit dem Hebel „Weniger kurze Abstände“.
        void Melden(Schnellstand s)
        {
            staende.Add(s);
            if (gewaehlt is null && s.Vorschlaege is { Count: > 0 } v && s.Sekunden > 10)
            {
                gewaehlt = v[0].Plan;
                steuerung.Waehlen(gewaehlt, 1);
            }
        }

        Schnellergebnis ergebnis = Schnellplaner.Planen(staffel, Berechnungsoptionen.Standard, samen => new Highsbruecke(), e, steuerung, Melden, abbruch.Token);

        Planguete guete = Assert.IsType<Planguete>(ergebnis.Guete);
        Assert.Equal(0, guete.Harte);
        Assert.NotNull(ergebnis.ErsterGueltigerPlan);
        Assert.NotNull(ergebnis.AUndBFertig);
        (_, Planguete nachAB) = Assert.IsType<(IReadOnlyList<Spiel>, Planguete)>(ergebnis.Grundplan);
        Assert.True(guete.A <= nachAB.A && guete.Wuensche <= nachAB.Wuensche, $"{guete} gegen {nachAB}");

        // Die Wahl gilt: Stufe 2, Fokus auf die Abstände, der beste Plan ist nicht schlechter in ihnen und bleibt nahe am gewählten.
        Grenzplan wahl = Assert.IsType<Grenzplan>(gewaehlt);
        Assert.Equal(2, ergebnis.Stufe);
        Assert.Equal(1, ergebnis.Massstab.Fokus);
        Assert.NotNull(ergebnis.Massstab.Bezug);
        Assert.Contains(ergebnis.Kandidaten, k => k.Gruppe == 1 && k.Aktiv);
        Assert.True(ergebnis.Massstab.Gruppe(guete, 1) <= ergebnis.Massstab.Gruppe(wahl.Guete, 1) + 1e-9);
        Assert.All(Massstab.Gruppen.SelectMany(g => g.Kriterien), k => Assert.True(
            guete.Anzahl[(int)k] <= wahl.Guete.Anzahl[(int)k] + Math.Max(1, (int)Math.Ceiling(wahl.Guete.Anzahl[(int)k] * e.Toleranz)),
            $"{k}: {guete.Anzahl[(int)k]} gegen {wahl.Guete.Anzahl[(int)k]}"));
        Assert.Contains(staende, s => s.Phase == 3);

        // Die Grenze der Stufe: nicht dominiert, nie schlechter in Pflicht und Wünschen; der beste Plan liegt auf ihr.
        Assert.NotEmpty(ergebnis.Grenze);
        Assert.All(ergebnis.Grenze, p => Assert.True(p.Guete.A <= nachAB.A && p.Guete.Wuensche <= nachAB.Wuensche));
        Assert.Contains(ergebnis.Grenze, p => ReferenceEquals(p.Plan, ergebnis.Plan));
        Assert.All(ergebnis.Grenze, p => Assert.DoesNotContain(ergebnis.Grenze, q => !ReferenceEquals(p, q) && q.Gruppen.Zip(p.Gruppen).All(z => z.First <= z.Second) && !q.Gruppen.SequenceEqual(p.Gruppen)));

        // Vorschläge und Beweisstand: Pflicht und Wünsche sind in R3 bewiesen (bzw. ohne Verstoß), die Empfehlung ist der beste Plan.
        IReadOnlyList<Vorschlag> vorschlaege = Assert.IsAssignableFrom<IReadOnlyList<Vorschlag>>(ergebnis.Vorschlaege);
        Assert.Same(ergebnis.Plan, vorschlaege[0].Plan.Plan);
        IReadOnlyList<Kriteriumsstatus> status = Assert.IsAssignableFrom<IReadOnlyList<Kriteriumsstatus>>(ergebnis.Status);
        Assert.All(status.Where(s => Planguete.Pflicht.Contains(s.Kriterium) || Planguete.Wunschkriterien.Contains(s.Kriterium)), s => Assert.True(s.Stand is Beweisstand.Optimal or Beweisstand.Bewiesen, $"{s}"));
        Planguete nachgerechnet = Planguete.Bewerten(Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = ergebnis.Plan }, Berechnungsoptionen.Standard));
        Assert.Equal(guete.Anzahl, nachgerechnet.Anzahl);
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
