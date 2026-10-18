// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Zielfunktion der Suche beim Glätten von C im Automodus (statt der Kosten des Originals): Sie zählt wie der Automodus –
/// zuerst die harten Fehler, dann die Verstöße in A, dann in B, dann in C (jeder Verstoß einer Stufe zählt gleich); die Kosten
/// des Originals entscheiden nur bei Gleichstand. So hat die Suche dasselbe Ziel wie der Vergleich der Pläne und wie
/// die Teilneuplanung – mit den Kosten des Originals wanderte sie zu Plänen, die mit den Gewichten billiger sind, aber mehr
/// Verstöße in C haben.
/// </summary>
internal sealed class Zaehlziel
{
    /// <summary>So viel zählt ein harter Fehler.</summary>
    private const double Hart = 1e9;

    /// <summary>So viel zählt ein Verstoß in A (mehr als jede übliche Zahl von Verstößen in B und C zusammen).</summary>
    private const double JeA = 1e7;

    /// <summary>So viel zählt ein Verstoß in B (mehr als jede übliche Zahl von Verstößen in C).</summary>
    private const double JeB = 1e5;

    /// <summary>Anteil der Kosten des Originals (nur zum Entscheiden bei Gleichstand; bleibt unter einem Verstoß).</summary>
    private const double Gleichstand = 1e-9;

    private readonly IReadOnlyList<Stufe> stufen;

    /// <summary>Initialisiert das Ziel.</summary>
    /// <param name="stufen">Stufe je Kriterium (Index = <see cref="Kostenkriterium"/>).</param>
    public Zaehlziel(IReadOnlyList<Stufe> stufen)
    {
        this.stufen = stufen;
    }

    /// <summary>Der Wert eines Plans nach seiner Kostenrechnung (die Anzahlen je Mannschaft und Art sind dann aktuell).</summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="kosten">Seine Kosten nach dem Original.</param>
    /// <param name="harte">Seine harten Fehler (Spiele ohne Termin, an spielfreien Tagen, nicht erlaubt).</param>
    /// <returns>Der Wert (kleiner ist besser).</returns>
    public double Wert(KernPlan plan, double kosten, int harte)
    {
        Berechnungsoptionen optionen = plan.D.Optionen;
        int a = 0;
        int b = 0;
        int c = 0;
        for (int art = 0; art < KernDefinition.AnzahlArten; art++)
        {
            if (optionen.Fuer((MannschaftsKostenart)art) == Gewichtung.NichtBeruecksichtigen || art >= stufen.Count)
            {
                continue;
            }

            int summe = 0;
            for (int t = 0; t < plan.D.N; t++)
            {
                summe += Math.Max(0, plan.KostenAnzahl[(t * KernDefinition.AnzahlArten) + art]);
            }

            switch (stufen[art])
            {
                case Stufe.A:
                    a += summe;
                    break;
                case Stufe.B:
                    b += summe;
                    break;
                default:
                    c += summe;
                    break;
            }
        }

        if (optionen.VereinsinterneSpieleAmAnfang != Gewichtung.NichtBeruecksichtigen && stufen[(int)Kostenkriterium.VereinsinterneSpieleAmAnfang] != Stufe.C)
        {
            a += plan.TeamsNichtAmAnfang();
        }

        return (Hart * harte) + (JeA * a) + (JeB * b) + c + Math.Min(0.49, kosten * Gleichstand);
    }
}
