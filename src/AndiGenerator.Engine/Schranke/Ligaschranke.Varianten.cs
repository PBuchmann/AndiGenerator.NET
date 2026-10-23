// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Nachbessern der B-Varianten mit der Teilneuplanung.</summary>
public sealed partial class Ligaschranke
{
    /// <summary>Zeitgrenze für HiGHS je Ausschnitt beim Nachbessern der Varianten.</summary>
    private static readonly TimeSpan VariantenJeAusschnitt = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Bessert die Varianten in B nach: HiGHS findet für „Vorrang …“ das Kriterium bestmöglich, die übrigen B-Kriterien in der
    /// Zeit aber oft nur schlecht (Verbandsoberliga: B 75 und 90, während der Plan für die Summe 37 hat). Die Teilneuplanung hält
    /// das Kriterium der Variante bei seinem Wert und senkt Ausschnitt für Ausschnitt erst B, dann C – wie nach einer Festlegung.
    /// Der Plan für die Summe in B wird ebenso nachgebessert (ohne Obergrenze), der Plan für A und B zusammen bleibt.
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="loeser">Der Löser (HiGHS).</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="bericht">Der Bericht der Schranke mit den Varianten.</param>
    /// <param name="jeVariante">Zeit je Variante (die Varianten rechnen gleichzeitig).</param>
    /// <param name="aufhoeren">Bricht ab (auch mitten in einem Lauf von HiGHS), oder <c>null</c>.</param>
    /// <returns>Der Bericht mit den nachgebesserten Varianten (eine bleibt, wenn sie nicht besser wurde).</returns>
    public static Schrankenbericht VariantenNachbessern(Staffel staffel, Berechnungsoptionen optionen, ISchrankenloeser loeser, Stufeneinteilung einteilung, Schrankenbericht bericht, TimeSpan jeVariante, Func<bool>? aufhoeren = null)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(bericht);
        ArgumentNullException.ThrowIfNull(einteilung);
        if (bericht.Alternativen is not { Count: > 0 } alternativen || aufhoeren?.Invoke() == true)
        {
            return bericht;
        }

        var neu = new Planalternative[alternativen.Count];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        System.Threading.Tasks.Parallel.For(0, alternativen.Count, parallel, i =>
        {
            Planalternative alternative = alternativen[i];
            neu[i] = alternative.AUndB || alternative.Plan.Count == 0 ? alternative : Nachbessern(alternative);
        });
        return bericht with { Alternativen = neu };

        Planalternative Nachbessern(Planalternative alternative)
        {
            IReadOnlyDictionary<Kostenkriterium, int>? obergrenzen = alternative.Kriterium is Kostenkriterium k
                ? new Dictionary<Kostenkriterium, int> { [k] = Kriteriumanzahl(k, Referenzbewertung.Bewerten(staffel with { BestehenderSpielplan = alternative.Plan }, optionen)) }
                : null;
            var e = new Neuplanungseinstellungen(jeVariante, VariantenJeAusschnitt, Obergrenzen: obergrenzen, Unterbrechen: aufhoeren);
            Neuplanungsbericht n = Neuplanen(staffel, alternative.Plan, optionen, loeser, einteilung, e);
            return Stufenvergleich.Besser(n.Nachher, n.Vorher) ? alternative with { Plan = n.Plan } : alternative;
        }
    }

    /// <summary>Die Zahl der Verstöße eines Kriteriums in einer Bewertung (wie <see cref="Stufeneinteilung.Zaehlen"/>).</summary>
    private static int Kriteriumanzahl(Kostenkriterium k, Planbewertung bewertung)
    {
        if (k != Kostenkriterium.VereinsinterneSpieleAmAnfang)
        {
            return bewertung.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)k].Anzahl));
        }

        return bewertung.VereinsinterneSpieleAmAnfang > 0 ? bewertung.VereinsinterneAnzahl : 0;
    }
}
