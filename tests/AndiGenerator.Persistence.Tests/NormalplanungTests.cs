// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Normaler Modus ohne Oberfläche: bester Plan bleibt erhalten, Probleme als Sätze mit Mannschaften.</summary>
public sealed class NormalplanungTests : IDisposable
{
    private const string R2 = "R2_4__Kreisklasse_Gruppe_A (2)";
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-normal-" + Guid.NewGuid().ToString("N"));

    public NormalplanungTests()
    {
        Directory.CreateDirectory(Path.Combine(ordner, "daten"));
        Directory.CreateDirectory(Path.Combine(ordner, "basis"));
    }

    public void Dispose() => Directory.Delete(ordner, recursive: true);

    [Fact]
    public void Bester_Plan_wird_gemerkt_und_nur_durch_einen_besseren_ersetzt()
    {
        Plansitzung sitzung = Oeffnen();
        IReadOnlyList<Spiel> clickTt = sitzung.Staffel.BestehenderSpielplan;
        Assert.NotEmpty(clickTt);
        Assert.Null(Normalplanung.BesterPlan(sitzung));
        Assert.Null(Normalplanung.Vergleichsplan(sitzung));

        (IReadOnlyList<Spiel> plan, Planguete guete, bool neu) = Normalplanung.Uebernehmen(sitzung, clickTt);
        Assert.True(neu);
        Assert.Same(clickTt, plan);
        Assert.Contains(sitzung.GemerktePlaene(), e => e.Name == Normalplanung.BesterPlanName);
        var bester = Normalplanung.BesterPlan(sitzung);
        Assert.NotNull(bester);
        Assert.Equal(guete.Anzahl, bester.Value.Guete.Anzahl);
        Assert.Equal(clickTt.Count, Normalplanung.Vergleichsplan(sitzung)!.Value.Plan.Count);

        // Ein gleich guter Plan ersetzt ihn nicht.
        (_, _, bool nochmal) = Normalplanung.Uebernehmen(sitzung, [.. clickTt]);
        Assert.False(nochmal);
    }

    [Fact]
    public void Probleme_nennen_Mannschaften_und_stehen_nach_Stufen_sortiert()
    {
        Plansitzung sitzung = Oeffnen();
        IReadOnlyList<Planproblem> probleme = Planprobleme.Erstellen(sitzung.Bewerten(sitzung.Staffel.BestehenderSpielplan));
        Assert.NotEmpty(probleme);
        Assert.Equal(probleme.OrderBy(p => p.Stufe).Select(p => p.Stufe), probleme.Select(p => p.Stufe));
        Assert.All(probleme, p => Assert.True(p.Anzahl > 0));
        Assert.All(probleme, p => Assert.False(string.IsNullOrWhiteSpace(p.Was)));

        string[] namen = [.. sitzung.Staffel.Mannschaften.Select(m => m.Name)];
        Planproblem mitNamen = probleme.First(p => p.Wer.Length > 0);
        Assert.Contains(namen, n => mitNamen.Wer.Contains(n, StringComparison.Ordinal));
        Assert.EndsWith(":", mitNamen.Wer, StringComparison.Ordinal);

        // Namen nur bei wenigen Mannschaften; sonst ein allgemeiner Satz ohne Namen.
        Assert.All(probleme, p => Assert.True(p.Wer.Split(',').Length <= Planprobleme.MannschaftenJeSatz));
        Assert.DoesNotContain(probleme, p => p.Wer.Contains("weitere", StringComparison.Ordinal));
    }

    [Fact]
    public void Unwichtige_Kriterien_erscheinen_nicht_unter_den_Problemen()
    {
        Plansitzung sitzung = Oeffnen();
        var bewertung = sitzung.Bewerten(sitzung.Staffel.BestehenderSpielplan);
        IReadOnlyList<Planproblem> alle = Planprobleme.Erstellen(bewertung);
        Assert.Contains(alle, p => p.Stufe == Stufe.C);

        Massstab leise = Massstab.Standard;
        foreach (Kostenkriterium k in Massstab.Gruppen.SelectMany(g => g.Kriterien))
        {
            leise = leise.MitFaktor(k, 0.15);
        }

        Assert.DoesNotContain(Planprobleme.Erstellen(bewertung, leise), p => p.Stufe == Stufe.C);
        Assert.Equal(alle.Count(p => p.Stufe != Stufe.C), Planprobleme.Erstellen(bewertung, leise).Count);
    }

    [Fact]
    public void Faktor_wirkt_auf_Gewicht_und_Gruppenwert_nur_des_einen_Kriteriums()
    {
        Massstab standard = Massstab.Standard;
        Assert.Equal(1, standard.Faktor(Kostenkriterium.WechselHeimAuswaerts));
        Massstab hoch = standard.MitFaktor(Kostenkriterium.WechselHeimAuswaerts, 6);
        Assert.Equal(6, hoch.Faktor(Kostenkriterium.WechselHeimAuswaerts));
        Assert.Equal(1, hoch.Faktor(Kostenkriterium.Spielverteilung));
        Assert.Equal(standard.Kriteriumsgewicht(Kostenkriterium.WechselHeimAuswaerts, 10) * 6, hoch.Kriteriumsgewicht(Kostenkriterium.WechselHeimAuswaerts, 10), 9);
        Assert.Equal(standard.Kriteriumsgewicht(Kostenkriterium.Spielverteilung, 10), hoch.Kriteriumsgewicht(Kostenkriterium.Spielverteilung, 10), 9);

        var steuerung = new Schnellsteuerung();
        steuerung.Wichtigkeit(Kostenkriterium.WechselHeimAuswaerts, 0.15);
        Assert.Equal(0.15, steuerung.Massstab.Faktor(Kostenkriterium.WechselHeimAuswaerts));
    }

    [Fact]
    public void Stufen_der_Kriterien_im_normalen_Modus()
    {
        Assert.Equal(Stufe.A, Planprobleme.StufeVon(Kostenkriterium.Pflichtspieltage));
        Assert.Equal(Stufe.B, Planprobleme.StufeVon(Kostenkriterium.Sperrtermine));
        Assert.Equal(Stufe.C, Planprobleme.StufeVon(Kostenkriterium.WechselHeimAuswaerts));
    }

    private Plansitzung Oeffnen()
    {
        string ziel = Path.Combine(ordner, "daten", "staffel.xml");
        File.Copy(Path.Combine(Testdaten.Referenz, "eingabe", R2 + ".xml"), ziel, overwrite: true);
        return Plansitzung.Oeffnen(ziel, Path.Combine(ordner, "basis"));
    }
}
