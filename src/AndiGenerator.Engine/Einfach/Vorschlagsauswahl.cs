// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Einfach;

/// <summary>
/// Die Vorschläge des einfachen Modus (Peter, 28.10.2026): wenige Pläne, die sich wesentlich unterscheiden, und bei jedem auf
/// einen Blick, wodurch. Zuerst die Empfehlung (der beste nach dem Maßstab), dann je offener Gruppe der Plan, der sie am besten
/// erfüllt (derselbe Plan nur einmal) – aber nur gute Pläne (ausgewogen höchstens doppelt so schlecht wie der beste) und nur,
/// wenn er von allen schon gewählten mindestens <c>schwelle</c> entfernt ist (gewichtete Summe der Gruppenunterschiede, in
/// Einheiten der Güte). Benannt ist jeder nach den Gruppen, in denen er am meisten gewinnt. Mit jeder Stufe sinkt die Schwelle:
/// Die Pläne werden ähnlicher.
/// </summary>
public static class Vorschlagsauswahl
{
    /// <summary>So viele Vorschläge höchstens.</summary>
    public const int Hoechstens = 4;

    /// <summary>So viele Unterschiede nennt ein Vorschlag höchstens.</summary>
    public const int UnterschiedeHoechstens = 4;

    /// <summary>Wie weit zwei Pläne auseinanderliegen: Summe der Gruppenunterschiede mit ihren Gewichten (Einheit der Güte).</summary>
    /// <param name="x">Der eine.</param>
    /// <param name="y">Der andere.</param>
    /// <param name="massstab">Der Maßstab (Gewichte).</param>
    /// <returns>Der Abstand (0 = gleiche Gruppenwerte).</returns>
    public static double Abstand(Grenzplan x, Grenzplan y, Massstab massstab)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(massstab);
        return Enumerable.Range(0, Massstab.Gruppen.Count).Sum(g => massstab.Gewichte[g] * Math.Abs(x.Gruppen[g] - y.Gruppen[g]));
    }

    /// <summary>Wählt die Vorschläge.</summary>
    /// <param name="plaene">Die Pläne an der Grenze (der Stufe).</param>
    /// <param name="massstab">Der Maßstab des Klassenleiters (mit Fokus: dem gewählten Hebel).</param>
    /// <param name="schwelle">So weit muss ein Vorschlag von allen anderen entfernt sein.</param>
    /// <param name="gewaehlt">Der zuletzt gewählte Plan (zum Vergleich), oder <c>null</c>.</param>
    /// <returns>Die Vorschläge, die Empfehlung zuerst; leer ohne Pläne.</returns>
    public static IReadOnlyList<Vorschlag> Waehlen(IReadOnlyList<Grenzplan> plaene, Massstab massstab, double schwelle, Grenzplan? gewaehlt = null)
    {
        ArgumentNullException.ThrowIfNull(plaene);
        ArgumentNullException.ThrowIfNull(massstab);
        if (plaene.Count == 0)
        {
            return [];
        }

        Grenzplan empfehlung = Bester(plaene, massstab);
        Massstab ausgewogen = massstab with { Fokus = null };
        double bestGuete = ausgewogen.Guete(Bester(plaene, ausgewogen).Guete);
        var anwaerter = new List<(string? Name, Grenzplan Plan)>();
        if (massstab.Fokus is not null)
        {
            anwaerter.Add(("Ausgewogen", Bester(plaene, ausgewogen)));
        }

        for (int g = 0; g < Massstab.Gruppen.Count; g++)
        {
            Grenzplan plan = Bester(plaene, ausgewogen with { Fokus = g });
            if (empfehlung.Gruppen[g] > 1e-9 && g != massstab.Fokus && !anwaerter.Exists(a => ReferenceEquals(a.Plan, plan)))
            {
                anwaerter.Add((null, plan));
            }
        }

        if (gewaehlt is not null)
        {
            anwaerter.Add(("Bisherige Wahl", gewaehlt));
        }

        var vorschlaege = new List<Vorschlag> { new(massstab.Fokus is int f ? "Empfehlung: " + Massstab.Gruppen[f].Hebel : "Empfehlung", massstab.Fokus, empfehlung, []) };
        foreach ((string? name, Grenzplan plan) in anwaerter)
        {
            if (vorschlaege.Count >= Hoechstens)
            {
                break;
            }

            // Nur gute Pläne (ausgewogen höchstens doppelt so schlecht wie der beste) und nur wesentlich verschiedene.
            if (ausgewogen.Guete(plan.Guete) <= (2 * bestGuete) + 0.1 && vorschlaege.TrueForAll(v => Abstand(v.Plan, plan, massstab) >= schwelle))
            {
                // Benannt nach den Gruppen, in denen er gegenüber der Empfehlung am meisten gewinnt (gewichtet).
                int[] besser = [.. Enumerable.Range(0, Massstab.Gruppen.Count)
                    .Where(g => plan.Gruppen[g] < empfehlung.Gruppen[g] - 1e-9)
                    .OrderByDescending(g => massstab.Gewichte[g] * (empfehlung.Gruppen[g] - plan.Gruppen[g]))
                    .Take(2)];
                string titel = name ?? (besser.Length == 0 ? "Alternative" : "Besser bei " + string.Join(" und ", besser.Select(g => Massstab.Gruppen[g].Name)));
                vorschlaege.Add(new Vorschlag(titel, besser.Length > 0 ? besser[0] : null, plan, Unterschiede(empfehlung.Guete, plan.Guete, massstab)));
            }
        }

        return vorschlaege;
    }

    /// <summary>
    /// Worin sich zwei Pläne unterscheiden: die Kriterien der Gruppen, deren Verstöße sich deutlich ändern (mindestens um 1 und
    /// um ein Zehntel) – erst, was besser wird, dann, was es kostet, jeweils die mit der größten Wirkung auf die Güte zuerst.
    /// </summary>
    /// <param name="von">Der Vergleichsplan (die Empfehlung).</param>
    /// <param name="nach">Der Vorschlag.</param>
    /// <param name="massstab">Der Maßstab (Gewichte und Bezug).</param>
    /// <returns>Höchstens <see cref="UnterschiedeHoechstens"/> Unterschiede.</returns>
    public static IReadOnlyList<Unterschied> Unterschiede(Planguete von, Planguete nach, Massstab massstab)
    {
        ArgumentNullException.ThrowIfNull(von);
        ArgumentNullException.ThrowIfNull(nach);
        ArgumentNullException.ThrowIfNull(massstab);
        List<Unterschied> alle = [.. Massstab.Gruppen.SelectMany(g => g.Kriterien)
            .Select(k => new Unterschied(k, von.Anzahl[(int)k], nach.Anzahl[(int)k]))
            .Where(u => Math.Abs(u.Nach - u.Von) >= Math.Max(1, 0.1 * Math.Max(u.Von, u.Nach)))
            .OrderByDescending(u => massstab.Kriteriumsgewicht(u.Kriterium, von.Mannschaften) * Math.Abs(u.Nach - u.Von))];

        // Höchstens die Hälfte für das, was besser wird, und für das, was es kostet – sonst verdecken große Kosten den Gewinn.
        List<Unterschied> besser = [.. alle.Where(u => u.Nach < u.Von).Take(UnterschiedeHoechstens / 2)];
        List<Unterschied> kosten = [.. alle.Where(u => u.Nach > u.Von).Take(UnterschiedeHoechstens - besser.Count)];
        return [.. besser, .. kosten, .. alle.Where(u => u.Nach < u.Von).Skip(besser.Count).Take(UnterschiedeHoechstens - besser.Count - kosten.Count)];
    }

    private static Grenzplan Bester(IReadOnlyList<Grenzplan> plaene, Massstab massstab)
    {
        Grenzplan bester = plaene[0];
        foreach (Grenzplan p in plaene.Skip(1).Where(p => massstab.IstBesser(p.Guete, bester.Guete)))
        {
            bester = p;
        }

        return bester;
    }
}
