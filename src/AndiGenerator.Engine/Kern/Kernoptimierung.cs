// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Ein Optimierungs-Worker (Original <c>TPlanCalcThread.Execute</c>) auf der schnellen Engine. Bei gleichem Startwert
/// durchläuft er exakt dieselben Zwischenstände wie <see cref="Referenzoptimierung"/>.
/// </summary>
public static class Kernoptimierung
{
    /// <summary>Optimiert die Staffel ausgehend von einem leeren Plan.</summary>
    /// <param name="staffel">Staffel; ein bestehender Spielplan wird wie im Original zunächst geleert.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <param name="strategie">Neu-Würfel-Strategie, z. B. <c>R</c>, <c>15</c>, <c>S1,25</c>, <c>M1,10</c>.</param>
    /// <param name="durchlaeufe">Anzahl der Durchläufe.</param>
    /// <param name="startwert">Startwert des Zufallsgenerators.</param>
    /// <returns>Der beste gefundene Plan.</returns>
    public static Optimierungsergebnis Optimieren(Staffel staffel, Berechnungsoptionen optionen, string strategie, int durchlaeufe, int startwert)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentException.ThrowIfNullOrEmpty(strategie);

        var zufall = new Zufall(startwert);
        Tauschstrategie tausch = Tauschstrategie.Lesen(strategie);
        var plan = new KernPlan(RefPlan.Laden(staffel, optionen));
        plan.TermineLeeren();
        plan.UngueltigeTermineEntfernen();

        double besteKosten = plan.Kosten();
        plan.Merken();
        int verbesserungen = 0;

        for (int durchlauf = 0; durchlauf < durchlaeufe; durchlauf++)
        {
            plan.Zuruecksetzen();
            plan.VorgegebeneSpieleSetzen();
            plan.NeuWuerfeln(tausch, zufall);
            plan.TermineFuellen(zufall);
            double kosten = plan.Kosten();
            if (kosten >= 0 && (kosten < besteKosten || besteKosten < 0))
            {
                plan.Merken();
                besteKosten = kosten;
                verbesserungen++;
            }
        }

        plan.Zuruecksetzen();
        plan.VorgegebeneSpieleSetzen();
        var spiele = new List<Spiel>(plan.AnzahlErlaubt);
        for (int s = 0; s < plan.AnzahlErlaubt; s++)
        {
            DateTime? zeitpunkt = plan.Datum[s] != 0.0 ? DelphiDatum.ZuDateTime(plan.Datum[s]) : null;
            spiele.Add(new Spiel(zeitpunkt, plan.D.Name[plan.Heim[s]], plan.D.Name[plan.Gast[s]], string.Empty));
        }

        return new Optimierungsergebnis(besteKosten, verbesserungen, spiele);
    }
}
