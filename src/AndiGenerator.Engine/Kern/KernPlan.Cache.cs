// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Kosten-Cache je Mannschaft und Kostenart (MIGRATIONSPLAN E7). Vor jeder Kostenrechnung wird der Stand aller Spiele und
/// Spiellisten mit dem Stand der letzten Rechnung verglichen; nur Mannschaften mit Änderungen werden neu gerechnet.
/// Hallenbelegung und parallele Spiele hängen zusätzlich von den Spielen der Vereinsmannschaften ab.
/// Die Summation läuft wie ohne Cache über alle Mannschaften in fester Reihenfolge, deshalb bleibt das Ergebnis bitgleich.
/// </summary>
internal sealed partial class KernPlan
{
    /// <summary>Gesamtkosten ohne Nutzung des Caches (für Tests und Messungen).</summary>
    public double KostenOhneCache()
    {
        Array.Clear(artBerechnet);
        return Kosten();
    }

    /// <summary>Vergleicht den aktuellen Stand mit dem Stand der letzten Kostenrechnung und markiert betroffene Mannschaften.</summary>
    private void AenderungenErmitteln()
    {
        Array.Clear(geaendert);
        int anzahl = Math.Max(anzahlAktiv, cacheAnzahlAktiv);
        for (int s = 0; s < anzahl; s++)
        {
            int lokal = Lokal(s);
            if (Datum[s] != cacheDatum[s] || Opt[s] != cacheOptionen[s] || MaxHeim[s] != cacheMaxHeim[s]
                || NichtNotwendig[s] != cacheNichtNotwendig[s] || lokal != cacheLokal[s])
            {
                geaendert[Heim[s]] = true;
                geaendert[Gast[s]] = true;
                cacheDatum[s] = Datum[s];
                cacheOptionen[s] = Opt[s];
                cacheMaxHeim[s] = MaxHeim[s];
                cacheNichtNotwendig[s] = NichtNotwendig[s];
                cacheLokal[s] = lokal;
            }
        }

        cacheAnzahlAktiv = anzahlAktiv;
        for (int t = 0; t < D.N; t++)
        {
            int spiele = SpieleAnzahl[t];
            if (spiele != cacheSpieleAnzahl[t] || !Spiele[t].AsSpan(0, spiele).SequenceEqual(cacheSpiele[t].AsSpan(0, spiele)))
            {
                geaendert[t] = true;
                Array.Copy(Spiele[t], cacheSpiele[t], spiele);
                cacheSpieleAnzahl[t] = spiele;
            }
        }

        for (int t = 0; t < D.N; t++)
        {
            bool betroffen = geaendert[t];
            foreach (int verein in D.Vereinsteams[t])
            {
                betroffen |= geaendert[verein];
            }

            vereinGeaendert[t] = betroffen;
        }
    }

    /// <summary>Original <c>CalculateMannschaftsKosten</c> mit Cache: nur geänderte Mannschaften werden neu gerechnet.</summary>
    private double MannschaftsKostenGecacht(MannschaftsKostenart art)
    {
        bool alle = !artBerechnet[(int)art];
        bool[] betroffen = art is MannschaftsKostenart.Hallenbelegung or MannschaftsKostenart.ParalleleSpiele ? vereinGeaendert : geaendert;
        double ergebnis = 0;
        for (int t = 0; t < D.N; t++)
        {
            if (alle || betroffen[t])
            {
                KostenBerechnen(t, art);
            }

            ergebnis += KostenWert[(t * KernDefinition.AnzahlArten) + (int)art];
        }

        artBerechnet[(int)art] = true;
        return ergebnis;
    }
}
