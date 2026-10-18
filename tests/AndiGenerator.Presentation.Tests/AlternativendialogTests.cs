// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Dialog der B-Varianten: unten die Unterschiede der ausgewählten Variante zum aktuellen Plan.</summary>
public sealed class AlternativendialogTests
{
    [Fact]
    public void Variante_zeigt_was_dazukommt_und_was_entfaellt()
    {
        Planstand aktuell = Stand(2);
        Planstand variante = Stand(1);
        var alternative = new Planalternative(Kostenkriterium.Sperrtermine, "Sperrtermine", variante.Spiele, Bewiesen: true);
        IReadOnlyList<Meldungsgruppe> Meldungen(Planstand plan, Kostenkriterium k) => ReferenceEquals(plan.Bewertung, aktuell.Bewertung)
            ? [new Meldungsgruppe("TTC Nord", "2 Verstöße", ["Sperrtermin: Sa 03.10.", "Sperrtermin: Sa 10.10."]), new Meldungsgruppe("SV Süd", "1 Verstoß", ["Sperrtermin: So 11.10."])]
            : [new Meldungsgruppe("TTC Nord", "1 Verstoß", ["Sperrtermin: Sa 10.10."]), new Meldungsgruppe("SV Süd", "1 Verstoß", ["Sperrtermin: So 11.10."]), new Meldungsgruppe("TuS West", "1 Verstoß", ["Sperrtermin: Sa 17.10."])];

        var dialog = new AlternativendialogViewModel(aktuell, [(alternative, variante.Bewertung)], alternative, Stufeneinteilung.AusText("A:Hallenbelegung;B:Sperrtermine"), Meldungen, true);

        // Gleiches (SV Süd, der 10.10. bei TTC Nord) fehlt; neu rot (+), entfallen grün (−).
        Vergleichsabschnitt abschnitt = Assert.Single(dialog.AuswahlDetails);
        Assert.Equal("B1 · Sperrtermine: 2 → 1", abschnitt.Titel);
        Assert.Equal(["TTC Nord", "TuS West"], abschnitt.Gruppen.Select(g => g.Name));
        Assert.Equal([("− Sperrtermin: Sa 03.10.", -1)], abschnitt.Gruppen[0].Zeilen.Select(z => (z.Text, z.Richtung)));
        Assert.Equal([("+ Sperrtermin: Sa 17.10.", 1)], abschnitt.Gruppen[1].Zeilen.Select(z => (z.Text, z.Richtung)));
        Assert.StartsWith("Unterschiede zum aktuellen Plan", dialog.DetailTitel, StringComparison.Ordinal);

        // Der aktuelle Plan selbst: seine Verstöße ohne Richtung.
        dialog.Spalten[0].AuswaehlenCommand.Execute(null);
        Assert.Equal("Betroffene Mannschaften – Aktueller Plan", dialog.DetailTitel);
        Assert.All(dialog.AuswahlDetails.SelectMany(a => a.Gruppen).SelectMany(g => g.Zeilen), z => Assert.Equal(0, z.Richtung));
    }

    [Fact]
    public void Ohne_Meldungen_steht_wenigstens_die_Anzahl()
    {
        Planstand aktuell = Stand(2);
        Planstand variante = Stand(5);
        var alternative = new Planalternative(Kostenkriterium.Sperrtermine, "Sperrtermine", variante.Spiele, Bewiesen: false);
        var dialog = new AlternativendialogViewModel(aktuell, [(alternative, variante.Bewertung)], alternative, Stufeneinteilung.AusText("A:Hallenbelegung;B:Sperrtermine"), (_, _) => [], true);

        Vergleichsabschnitt abschnitt = Assert.Single(dialog.AuswahlDetails);
        Assert.Equal("B1 · Sperrtermine: 2 → 5", abschnitt.Titel);
        Assert.Equal([("2 → 5 Verstöße (keine Einzelheiten)", 1)], Assert.Single(abschnitt.Gruppen).Zeilen.Select(z => (z.Text, z.Richtung)));
        Assert.DoesNotContain("keine", dialog.DetailTitel, StringComparison.Ordinal);
    }

    private static Planstand Stand(int sperrtermine)
    {
        Kostenzelle[] zellen = Enum.GetValues<MannschaftsKostenart>().Select(k => new Kostenzelle(k == MannschaftsKostenart.Sperrtermine ? sperrtermine : 0, 0, 0)).ToArray();
        var bewertung = new Planbewertung(0, 0, 0, 0, 0, 0, 0, 0, 0, [], [new Mannschaftsbewertung("TTC Nord", zellen, 0, [])]);
        return new Planstand([], bewertung);
    }
}
