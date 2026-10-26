// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;

namespace AndiGenerator.Engine.Einfach;

/// <summary>Ein Kriterium des besten Plans mit dem, was über es bewiesen ist.</summary>
/// <param name="Kriterium">Das Kriterium.</param>
/// <param name="Anzahl">Verstöße im Plan.</param>
/// <param name="Untergrenze">So viele hat jeder Plan mindestens (aus HiGHS), oder <c>null</c>, wenn nichts bekannt ist.</param>
/// <param name="Stand">Optimal, bewiesen, allein bewiesen oder offen.</param>
public sealed record Kriteriumsstatus(Kostenkriterium Kriterium, int Anzahl, int? Untergrenze, Beweisstand Stand)
{
    /// <summary>
    /// Der Stand je Kriterium (Pflicht, Wünsche und die Kriterien der Gruppen): ohne Verstoß optimal; Pflicht und Wünsche
    /// bewiesen, wenn HiGHS die Summe ihrer Stufe bewiesen hat und der Plan sie erreicht; sonst allein bewiesen, wenn der Plan
    /// die Untergrenze des Kriteriums allein erreicht; sonst offen.
    /// </summary>
    /// <param name="guete">Die Verstöße des Plans.</param>
    /// <param name="bericht">Die Schranke aus Phase 1, oder <c>null</c> (ohne HiGHS).</param>
    /// <returns>Je Kriterium der Stand.</returns>
    public static IReadOnlyList<Kriteriumsstatus> Bestimmen(Planguete guete, Schrankenbericht? bericht)
    {
        ArgumentNullException.ThrowIfNull(guete);
        var untergrenze = new Dictionary<Kostenkriterium, int>();
        foreach (Schrankenstufe s in bericht?.Stufen ?? [])
        {
            if (s.Kriterium is Kostenkriterium k)
            {
                untergrenze[k] = Math.Max(untergrenze.GetValueOrDefault(k), s.Schranke);
            }
        }

        foreach (Kriteriumsprobe p in bericht?.Proben ?? [])
        {
            untergrenze[p.Kriterium] = Math.Max(untergrenze.GetValueOrDefault(p.Kriterium), p.Schranke);
        }

        bool pflichtBewiesen = bericht?.SchrankeA is int a && guete.Harte == 0 && guete.A <= a;
        bool wuenscheBewiesen = pflichtBewiesen && bericht?.SchrankeB is int b && guete.Wuensche <= b;
        IEnumerable<Kostenkriterium> alle = Planguete.Pflicht.Concat(Planguete.Wunschkriterien).Concat(Massstab.Gruppen.SelectMany(g => g.Kriterien));
        return [.. alle.Select(k =>
        {
            int anzahl = guete.Anzahl[(int)k];
            int? grenze = untergrenze.TryGetValue(k, out int u) ? u : null;
            bool stufeBewiesen = (Planguete.Pflicht.Contains(k) && pflichtBewiesen) || (Planguete.Wunschkriterien.Contains(k) && wuenscheBewiesen);
            return new Kriteriumsstatus(k, anzahl, grenze, Einstufen(anzahl, stufeBewiesen, grenze));
        })];
    }

    private static Beweisstand Einstufen(int anzahl, bool stufeBewiesen, int? grenze)
    {
        if (anzahl == 0)
        {
            return Beweisstand.Optimal;
        }

        if (stufeBewiesen)
        {
            return Beweisstand.Bewiesen;
        }

        return grenze is int g && anzahl <= g ? Beweisstand.AlleinBewiesen : Beweisstand.Offen;
    }
}
