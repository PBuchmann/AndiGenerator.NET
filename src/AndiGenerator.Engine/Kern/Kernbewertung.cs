// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Bewertet einen Plan mit der schnellen Engine (MIGRATIONSPLAN E7). Liefert dieselben Anzahlen und Kosten wie
/// <see cref="Referenzbewertung"/>, aber ohne Klartextmeldungen; die Meldungen für die Oberfläche kommen aus dem Referenzmodell.
/// </summary>
public static class Kernbewertung
{
    /// <summary>Bewertet den bestehenden Spielplan der Staffel.</summary>
    /// <param name="staffel">Staffel mit dem zu bewertenden Plan als bestehendem Spielplan.</param>
    /// <param name="optionen">Berechnungsoptionen.</param>
    /// <returns>Die Bewertung (Meldungen leer).</returns>
    public static Planbewertung Bewerten(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel, optionen);
        var kern = new KernPlan(plan);
        RefPlan.Spieltagskosten spieltag = kern.SpieltagsKosten();

        var mannschaften = new List<Mannschaftsbewertung>();
        for (int t = 0; t < kern.D.N; t++)
        {
            var zellen = new List<Kostenzelle>();
            double gesamt = 0.0;
            foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
            {
                kern.KostenFuerArt(t, art);
                int k = (t * KernDefinition.AnzahlArten) + (int)art;
                zellen.Add(new Kostenzelle(kern.KostenAnzahl[k], kern.KostenAlle[k], kern.KostenWert[k]));
                gesamt += kern.KostenWert[k];
            }

            mannschaften.Add(new Mannschaftsbewertung(kern.D.Name[t], zellen, gesamt, []));
        }

        return new Planbewertung(
            kern.Kosten(),
            kern.FehlterminKosten(),
            kern.NichtErlaubteKosten(),
            kern.SpielfreieTageKosten(),
            spieltag.Ueberlappung,
            spieltag.Breite,
            spieltag.LetzteUeberlappung,
            spieltag.LetzteBreite,
            kern.VereinsinterneSpieleAmAnfangKosten(),
            Enum.GetValues<MannschaftsKostenart>().Where(plan.HatWerteFuer).ToList(),
            mannschaften);
    }
}
