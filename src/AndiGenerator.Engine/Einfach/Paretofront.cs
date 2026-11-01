// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Die Grenze des Machbaren (Peter, 28.10.2026, Bild der Wippe): Pläne mit bester Pflicht und besten Wünschen, von denen keiner
/// in allen Gruppen mindestens so gut ist wie ein anderer und in einer besser (nicht dominiert). Drückt der Klassenleiter eine
/// Gruppe herunter und geht keine andere hoch, war er noch nicht an der Grenze; an ihr kostet jede Senkung anderswo etwas.
/// Der einfache Modus sucht immer an dieser Grenze, sie zeigt, was eine Verbesserung kostet. Threadsicher.
/// </summary>
public sealed class Paretofront
{
    /// <summary>So viele Pläne behält die Grenze höchstens (bei mehr fällt der heraus, der seinem Nachbarn am nächsten ist).</summary>
    public const int Hoechstens = 60;

    private readonly Lock sperre = new();
    private readonly List<Grenzplan> plaene = [];

    /// <summary>Gets die Pläne an der Grenze (Kopie).</summary>
    public IReadOnlyList<Grenzplan> Plaene
    {
        get
        {
            lock (sperre)
            {
                return [.. plaene];
            }
        }
    }

    /// <summary>Nimmt einen Plan auf, wenn er nicht dominiert ist; dominierte fallen heraus.</summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="guete">Seine Verstöße.</param>
    /// <param name="massstab">Der Maßstab (für den Bezug der Gruppen).</param>
    /// <returns><c>true</c>, wenn er an der Grenze liegt.</returns>
    public bool Aufnehmen(IReadOnlyList<Spiel> plan, Planguete guete, Massstab massstab)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(guete);
        ArgumentNullException.ThrowIfNull(massstab);
        var neu = new Grenzplan(plan, guete, [.. Enumerable.Range(0, Massstab.Gruppen.Count).Select(g => massstab.Gruppe(guete, g))]);
        lock (sperre)
        {
            // Nur die beste Pflicht und die besten Wünsche zählen (sie werden nie getauscht).
            if (plaene.Count > 0)
            {
                int vergleich = (guete.Harte, guete.A, guete.Wuensche).CompareTo((plaene[0].Guete.Harte, plaene[0].Guete.A, plaene[0].Guete.Wuensche));
                if (vergleich > 0)
                {
                    return false;
                }

                if (vergleich < 0)
                {
                    plaene.Clear();
                }
            }

            if (plaene.Exists(p => Dominiert(p.Gruppen, neu.Gruppen) || p.Gruppen.SequenceEqual(neu.Gruppen)))
            {
                return false;
            }

            plaene.RemoveAll(p => Dominiert(neu.Gruppen, p.Gruppen));
            plaene.Add(neu);
            if (plaene.Count > Hoechstens)
            {
                plaene.RemoveAt(Naechster());
            }

            return true;
        }
    }

    /// <summary>
    /// Die Wippe: Was kostet es, eine Gruppe zu senken? Unter den Plänen der Grenze, in denen die Gruppe niedriger ist als im
    /// aktuellen, der mit dem kleinsten Anstieg der übrigen (gewichtet nach dem Maßstab).
    /// </summary>
    /// <param name="aktuell">Die Gruppenwerte des aktuellen Plans (siehe <see cref="Grenzplan.Gruppen"/>).</param>
    /// <param name="gruppe">Die Gruppe, die sinken soll.</param>
    /// <param name="massstab">Der Maßstab (Gewichte).</param>
    /// <returns>Der Plan und je Gruppe die Änderung; <c>null</c>, wenn die Grenze keinen Plan mit niedrigerer Gruppe kennt.</returns>
    public (Grenzplan Plan, IReadOnlyList<double> Aenderung)? Senken(IReadOnlyList<double> aktuell, int gruppe, Massstab massstab)
    {
        ArgumentNullException.ThrowIfNull(aktuell);
        ArgumentNullException.ThrowIfNull(massstab);
        lock (sperre)
        {
            Grenzplan? bester = null;
            double kosten = double.MaxValue;
            foreach (Grenzplan p in plaene.Where(p => p.Gruppen[gruppe] < aktuell[gruppe] - 1e-9))
            {
                double anstieg = Enumerable.Range(0, aktuell.Count).Where(g => g != gruppe).Sum(g => massstab.Gewichte[g] * Math.Max(0, p.Gruppen[g] - aktuell[g]));
                if (anstieg < kosten)
                {
                    kosten = anstieg;
                    bester = p;
                }
            }

            return bester is null ? null : (bester, [.. bester.Gruppen.Select((w, g) => w - aktuell[g])]);
        }
    }

    private static bool Dominiert(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        bool echt = false;
        for (int g = 0; g < a.Count; g++)
        {
            if (a[g] > b[g] + 1e-9)
            {
                return false;
            }

            echt |= a[g] < b[g] - 1e-9;
        }

        return echt;
    }

    /// <summary>Der Plan, der seinem nächsten Nachbarn am nächsten liegt (er trägt am wenigsten zur Grenze bei).</summary>
    private int Naechster()
    {
        int index = 0;
        double kleinster = double.MaxValue;
        for (int i = 0; i < plaene.Count; i++)
        {
            for (int j = 0; j < plaene.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                double abstand = plaene[i].Gruppen.Zip(plaene[j].Gruppen).Sum(p => Math.Abs(p.First - p.Second));
                if (abstand < kleinster)
                {
                    kleinster = abstand;
                    index = i;
                }
            }
        }

        return index;
    }
}
