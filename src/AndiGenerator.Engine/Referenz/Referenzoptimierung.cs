// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>
/// Ein Optimierungs-Worker des Originals (<c>TPlanCalcThread.Execute</c>) einsträngig und mit festem Startwert:
/// Rücksetzen auf den besten Stand, vorgegebene Spiele setzen, neu würfeln, Termine füllen, Kosten vergleichen (strikt kleiner).
/// Dient als Maßstab für die schnelle Engine; die Insel- und Optimiererebene des Originals ist hier nicht enthalten.
/// </summary>
public static class Referenzoptimierung
{
    /// <summary>Optimiert die Staffel ausgehend von einem leeren Plan (wie der Programmstart im Original).</summary>
    /// <param name="staffel">Staffel; ein bestehender Spielplan wird wie im Original zunächst geleert.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <param name="strategie">Neu-Würfel-Strategie, z. B. <c>R</c>, <c>15</c>, <c>S1,25</c>, <c>M1,10</c>.</param>
    /// <param name="durchlaeufe">Anzahl der Durchläufe.</param>
    /// <param name="startwert">Startwert des Zufallsgenerators (gleicher Startwert = gleiches Ergebnis).</param>
    /// <returns>Der beste gefundene Plan.</returns>
    public static Optimierungsergebnis Optimieren(Staffel staffel, Berechnungsoptionen optionen, string strategie, int durchlaeufe, int startwert)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        ArgumentException.ThrowIfNullOrEmpty(strategie);

        var zufall = new Zufall(startwert);
        Tauschstrategie tausch = Tauschstrategie.Lesen(strategie);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
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
        List<Spiel> spiele = plan.SpieleErlaubt
            .Select(s => new Spiel(s.DatumGueltig ? DelphiDatum.ZuDateTime(s.Datum) : null, s.Heim.TeamName, s.Gast.TeamName, string.Empty))
            .ToList();
        return new Optimierungsergebnis(besteKosten, verbesserungen, spiele);
    }
}
