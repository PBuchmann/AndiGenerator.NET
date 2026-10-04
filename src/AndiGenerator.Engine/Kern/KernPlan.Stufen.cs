// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>Bewertung nach Kriterien und Stufen für den Automodus (MIGRATIONSPLAN Abschnitt 11).</summary>
internal sealed partial class KernPlan
{
    /// <summary>
    /// Bewertet den aktuellen Stand je Kriterium (Anzahl der Verstöße und Kosten) und fasst nach Stufen zusammen; danach
    /// stehen in <see cref="KostenAnzahl"/> die Verstöße je Mannschaft.
    /// </summary>
    /// <param name="stufen">Stufe je Kriterium (Index = <see cref="Kostenkriterium"/>).</param>
    /// <param name="anzahl">Wird gefüllt: Verstöße je Kriterium.</param>
    /// <param name="kosten">Wird gefüllt: Kosten je Kriterium.</param>
    /// <returns>Harte Fehler, Verstöße A, B und C, Kosten der Stufe C.</returns>
    public Stufenwert StufenBewerten(Stufe[] stufen, double[] anzahl, double[] kosten)
    {
        Sortieren();
        Kosten();
        double hart = FehlterminKosten() + SpielfreieTageKosten() + NichtErlaubteKosten();
        int harte = (int)Math.Round(hart / (10.0 * RefPlan.MaxKostenOhneHartenFehler));
        Array.Clear(anzahl);
        Array.Clear(kosten);
        Berechnungsoptionen optionen = D.Optionen;
        foreach (MannschaftsKostenart art in Enum.GetValues<MannschaftsKostenart>())
        {
            if (optionen.Fuer(art) == Gewichtung.NichtBeruecksichtigen)
            {
                continue;
            }

            for (int t = 0; t < D.N; t++)
            {
                int k = (t * KernDefinition.AnzahlArten) + (int)art;
                anzahl[(int)art] += Math.Max(0, KostenAnzahl[k]);
                kosten[(int)art] += KostenWert[k];
            }
        }

        if (optionen.VereinsinterneSpieleAmAnfang != Gewichtung.NichtBeruecksichtigen)
        {
            double vereinsintern = VereinsinterneSpieleAmAnfangKosten();
            kosten[(int)Kostenkriterium.VereinsinterneSpieleAmAnfang] = vereinsintern;
            anzahl[(int)Kostenkriterium.VereinsinterneSpieleAmAnfang] = vereinsintern > 0 ? 1 : 0;
        }

        if (optionen.Spieltag != Gewichtung.NichtBeruecksichtigen || optionen.SpieltagUeberlappung != Gewichtung.NichtBeruecksichtigen)
        {
            RefPlan.Spieltagskosten s = SpieltagsKosten();
            kosten[(int)Kostenkriterium.Spieltaglaenge] = s.Breite;
            kosten[(int)Kostenkriterium.Spieltagueberlappung] = s.Ueberlappung;
            kosten[(int)Kostenkriterium.LetzterSpieltagLaenge] = s.LetzteBreite;
            kosten[(int)Kostenkriterium.LetzterSpieltagUeberlappung] = s.LetzteUeberlappung;
        }

        double[] jeStufe = new double[3];
        double kostenC = 0;
        for (int k = 0; k < stufen.Length; k++)
        {
            jeStufe[(int)stufen[k]] += anzahl[k];
            if (stufen[k] == Stufe.C)
            {
                kostenC += kosten[k];
            }
        }

        return new Stufenwert(harte, (int)jeStufe[0], (int)jeStufe[1], 0, (int)jeStufe[2], kostenC);
    }

    /// <summary>Vereinsinterne Spiele liegen nicht alle am Anfang (und die Kostenart wird berücksichtigt).</summary>
    /// <returns><c>true</c> bei einem Verstoß.</returns>
    public bool VereinsinternVerletzt() =>
        D.Optionen.VereinsinterneSpieleAmAnfang != Gewichtung.NichtBeruecksichtigen && TeamsNichtAmAnfang() > 0;
}
