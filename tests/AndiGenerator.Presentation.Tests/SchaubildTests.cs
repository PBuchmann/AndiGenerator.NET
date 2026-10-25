// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Schaubild im Leitstand: grober Zustand der Inseln und Ereignisse aus dem Vergleich zweier Stände.</summary>
public sealed class SchaubildTests
{
    [Fact]
    public void Nur_ein_besserer_bester_Plan_wandert_und_ein_Neustart_leuchtet_auf()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 3), Insel(2, 0, 5)]), []);
        Assert.Equal(2, bild.Kerne);
        Assert.Equal(2, bild.Inseln.Count);
        Assert.Empty(bild.Impulse);

        bild.Uebernehmen(Stand(2, [Insel(1, 0, 4), Insel(2, 1, 6)]), []);
        Assert.Single(bild.Impulse, i => i.Art == Schaubildereignis.BesterPlan);
        Assert.Contains(bild.Impulse, i => i.Art == Schaubildereignis.Neustart && i.Insel == 1);
        Assert.Equal("kommt voran", bild.Inseln[0].Taetigkeit);
        Assert.Equal("beginnt neu", bild.Inseln[1].Taetigkeit);
    }

    [Fact]
    public void Verbessert_sich_nur_eine_Insel_wandert_nichts()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 3)]), []);
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 4)]), []);
        Assert.DoesNotContain(bild.Impulse, i => i.Art == Schaubildereignis.BesterPlan);
        Assert.Equal("kommt voran", bild.Inseln[0].Taetigkeit);
    }

    [Fact]
    public void Eine_Insel_kurz_vor_dem_Neustart_steckt_fest()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 0), Insel(2, 0, 0, ohneVerbesserung: 80_000)]), []);
        Assert.Equal("sucht", bild.Inseln[0].Taetigkeit);
        Assert.Equal("findet lange nichts", bild.Inseln[1].Taetigkeit);
        Assert.Equal(0.8, bild.Inseln[1].Stillstand, 3);
    }

    [Fact]
    public void Ueber_der_Grenze_beginnt_nur_die_schlechteste_Insel_neu()
    {
        var bild = new Schaubild();
        Inselstand schlechteste = Insel(1, 0, 0, ohneVerbesserung: 150_000) with { Schlechteste = true };
        Inselstand andere = Insel(2, 0, 0, ohneVerbesserung: 300_000);
        bild.Uebernehmen(Stand(1, [schlechteste, andere]), []);
        Assert.Equal("beginnt gleich neu", bild.Inseln[0].Taetigkeit);
        Assert.Equal("steckt fest, rechnet weiter", bild.Inseln[1].Taetigkeit);
        Assert.Equal(1, bild.Inseln[1].Stillstand);
    }

    [Fact]
    public void Die_schlechteste_Insel_ueber_der_Grenze_beginnt_gleich_neu_auch_wenn_sie_eben_verbessert_hat()
    {
        var bild = new Schaubild();
        Inselstand schlechteste = Insel(1, 0, 3, ohneVerbesserung: 150_000) with { Schlechteste = true };
        Inselstand andere = Insel(2, 0, 3, ohneVerbesserung: 150_000);
        bild.Uebernehmen(Stand(1, [schlechteste, andere]), []);
        bild.Uebernehmen(Stand(1, [schlechteste with { Erfolge = 4 }, andere with { Erfolge = 4 }]), []);
        Assert.Equal("beginnt gleich neu", bild.Inseln[0].Taetigkeit);
        Assert.Equal("kommt voran", bild.Inseln[1].Taetigkeit);
    }

    [Fact]
    public void In_der_Pause_entstehen_keine_Ereignisse()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 1)]), []);
        bild.Uebernehmen(Stand(2, [Insel(1, 1, 2)]) with { Pausiert = true }, []);
        Assert.False(bild.Rechnet);
        Assert.Empty(bild.Impulse);
    }

    [Fact]
    public void Bei_der_Kostenoptimierung_stehen_die_groessten_Kostendifferenzen()
    {
        Kriterienwert[] alt = [new(Kostenkriterium.Sperrtermine, 1000), new(Kostenkriterium.Spielverteilung, 200), new(Kostenkriterium.Hallenbelegung, 50)];
        Kriterienwert[] neu = [new(Kostenkriterium.Sperrtermine, 400), new(Kostenkriterium.Spielverteilung, 300), new(Kostenkriterium.Hallenbelegung, 50)];

        IReadOnlyList<Planaenderung> aenderungen = Schaubild.Vergleichen(alt, neu);

        Assert.Equal(2, aenderungen.Count);
        Assert.True(aenderungen[0].Besser);
        Assert.EndsWith("−600", aenderungen[0].Text, StringComparison.Ordinal);
        Assert.False(aenderungen[1].Besser);
        Assert.EndsWith("+100", aenderungen[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Im_Automodus_steht_die_wichtigste_Stufe_zuerst()
    {
        Kriterienwert[] alt = [new(Kostenkriterium.Sperrtermine, 3, Stufe.C), new(Kostenkriterium.Hallenbelegung, 2, Stufe.A)];
        Kriterienwert[] neu = [new(Kostenkriterium.Sperrtermine, 9, Stufe.C), new(Kostenkriterium.Hallenbelegung, 1, Stufe.A)];

        IReadOnlyList<Planaenderung> aenderungen = Schaubild.Vergleichen(alt, neu);

        Assert.Equal(2, aenderungen.Count);
        Assert.StartsWith("A · ", aenderungen[0].Text, StringComparison.Ordinal);
        Assert.EndsWith("2 → 1", aenderungen[0].Text, StringComparison.Ordinal);
        Assert.True(aenderungen[0].Besser);
        Assert.StartsWith("C · ", aenderungen[1].Text, StringComparison.Ordinal);
        Assert.False(aenderungen[1].Besser);
    }

    [Fact]
    public void Kosten_und_Verstoesse_werden_nicht_verglichen()
    {
        Kriterienwert[] kosten = [new(Kostenkriterium.Sperrtermine, 1000)];
        Kriterienwert[] verstoesse = [new(Kostenkriterium.Sperrtermine, 3, Stufe.A)];
        Assert.Empty(Schaubild.Vergleichen(kosten, verstoesse));
        Assert.Empty(Schaubild.Vergleichen(null, verstoesse));
    }

    [Fact]
    public void Ein_besserer_bester_Plan_zeigt_was_sich_geaendert_hat()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 0)], [new(Kostenkriterium.Sperrtermine, 1000)]), []);
        Assert.Empty(bild.Meldungen);

        bild.Uebernehmen(Stand(2, [Insel(1, 0, 1)], [new(Kostenkriterium.Sperrtermine, 800)]), []);
        Verbesserungsmeldung meldung = Assert.Single(bild.Meldungen);
        Assert.Equal("Verbesserung 2", meldung.Titel);
        Planaenderung aenderung = Assert.Single(meldung.Aenderungen);
        Assert.True(aenderung.Besser);
        Assert.Single(bild.Impulse, i => i.Art == Schaubildereignis.BesterPlan);
    }

    [Fact]
    public void Jeder_gruene_Punkt_traegt_seine_eigene_Aenderung()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 0)], [new(Kostenkriterium.Sperrtermine, 1000)]), []);
        bild.Uebernehmen(Stand(2, [Insel(1, 0, 1)], [new(Kostenkriterium.Sperrtermine, 800)]), []);
        bild.Uebernehmen(Stand(3, [Insel(1, 0, 2)], [new(Kostenkriterium.Sperrtermine, 600)]), []);

        Assert.Equal(2, bild.Impulse.Count(i => i.Art == Schaubildereignis.BesterPlan));
        Assert.Equal(2, bild.Meldungen.Count);
        Assert.EndsWith("−200", Assert.Single(bild.Meldungen[0].Aenderungen).Text, StringComparison.Ordinal);
        Assert.EndsWith("−200", Assert.Single(bild.Meldungen[1].Aenderungen).Text, StringComparison.Ordinal);
        Assert.Equal(bild.Impulse.Last(i => i.Art == Schaubildereignis.BesterPlan).Zeitpunkt, bild.Meldungen[1].Zeitpunkt);
    }

    [Fact]
    public void Mehrere_Verbesserungen_zwischen_zwei_Staenden_nennen_alle_Nummern()
    {
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(4, [Insel(1, 0, 0)], [new(Kostenkriterium.Sperrtermine, 1000)]), []);
        bild.Uebernehmen(Stand(7, [Insel(1, 0, 1)], [new(Kostenkriterium.Sperrtermine, 700)]), []);
        Assert.Equal("Verbesserungen 5–7", Assert.Single(bild.Meldungen).Titel);
        Assert.Single(bild.Impulse, i => i.Art == Schaubildereignis.BesterPlan);
    }

    [Fact]
    public void Zu_jedem_gruenen_Punkt_erscheint_eine_Aenderung()
    {
        // Auch wenn sich kein Kriterium sichtbar ändert (Feinschliff), steht etwas unter dem besten Plan.
        var bild = new Schaubild();
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 0)], [new(Kostenkriterium.Sperrtermine, 3, Stufe.A)]), []);
        bild.Uebernehmen(Stand(2, [Insel(1, 0, 1)], [new(Kostenkriterium.Sperrtermine, 3, Stufe.A)]), []);
        Assert.Single(bild.Impulse, i => i.Art == Schaubildereignis.BesterPlan);
        Assert.Single(bild.Meldungen);
    }

    [Fact]
    public void Beim_Wechsel_von_Kosten_zu_Verstoessen_wandert_kein_Punkt()
    {
        var bild = new Schaubild();

        // Der Wechsel vom Grundlauf zur Lenkung zählt nicht als Verbesserung: Die Nummer bleibt gleich.
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 0)], [new(Kostenkriterium.Sperrtermine, 1000)]), []);
        bild.Uebernehmen(Stand(1, [Insel(1, 0, 1)], [new(Kostenkriterium.Sperrtermine, 3, Stufe.A)]), []);
        Assert.DoesNotContain(bild.Impulse, i => i.Art == Schaubildereignis.BesterPlan);

        // Die nächste Verbesserung vergleicht Verstöße mit Verstößen.
        bild.Uebernehmen(Stand(2, [Insel(1, 0, 2)], [new(Kostenkriterium.Sperrtermine, 2, Stufe.A)]), []);
        Assert.EndsWith("3 → 2", Assert.Single(Assert.Single(bild.Meldungen).Aenderungen).Text, StringComparison.Ordinal);
    }

    private static Inselstand Insel(int nummer, int neustarts, int erfolge, long ohneVerbesserung = 10) =>
        new(nummer, false, true, 1000, 1000, ohneVerbesserung, neustarts, ["R", "15"], Erfolge: erfolge);

    /// <summary>Ein Stand; die Nummer des besten Plans ist die Zahl der Verbesserungen.</summary>
    private static Optimierungsstand Stand(int verbesserungen, Inselstand[] inseln, Kriterienwert[]? besterPlan = null) =>
        new(true, false, 1000, 100, 900, verbesserungen, TimeSpan.Zero, null, [], null, new Maschinenraum(
            [500, 500],
            inseln,
            100_000,
            null,
            0,
            besterPlan ?? [new Kriterienwert(Kostenkriterium.Sperrtermine, 1000.0 / verbesserungen)],
            verbesserungen));
}
