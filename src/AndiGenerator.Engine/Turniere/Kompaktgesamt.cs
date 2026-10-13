// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Gesamtwertung eines Plans aus Kompaktspieltagen (Peter, 13.10.2026: möglichst viele Ziele möglichst gut). Kleiner ist
/// besser; ein Punkt entspricht jeweils: 10 % mehr Kilometer als der kürzeste Plan, ein Spieltag Rückstand im Stand
/// (2 Spiele über dem kleinsten möglichen Abstand), kein gemeinsamer Spieltag mit zwei 4er-Spieltagen; drei Punkte ein
/// fehlender Spitzen- bzw. Abstiegsspieltag, liegt einer früh in der Halbrunde, bis zu einem halben Punkt. Ungleich weite Fahrten:
/// ein Punkt je ein Viertel der mittleren Kilometer einer Mannschaft, um die der Unterschied zwischen der Mannschaft mit
/// den meisten und der mit den wenigsten Kilometern über dem kleinsten möglichen liegt. Verstöße gegen die
/// Meldungen, Mannschaften ohne Heimspieltag und 3er-Spieltage wiegen deutlich schwerer.
/// </summary>
internal static class Kompaktgesamt
{
    /// <summary>Die Wertung.</summary>
    /// <param name="l">Der Plan.</param>
    /// <param name="kmMin">Kilometer des kürzesten Plans.</param>
    /// <param name="gemeinsamZiel">So viele Mannschaften sollen mindestens einmal am selben Tag spielen.</param>
    /// <param name="spanneMin">Kleinster Unterschied der Kilometer zwischen den Mannschaften unter den verglichenen Plänen.</param>
    /// <param name="kmJeMannschaft">Mittlere Kilometer einer Mannschaft (Maßstab für den Unterschied).</param>
    /// <returns>Punkte, kleiner ist besser.</returns>
    public static double Wertung(Kompaktloesung l, double kmMin, int gemeinsamZiel, double spanneMin, double kmJeMannschaft)
    {
        double km = kmMin > 0 ? Math.Max(0, (l.Bewertungskm / kmMin) - 1) / 0.1 : 0;
        double stand = Math.Max(0, l.Abstand - 2) / 2.0;
        double gemeinsam = l.Gemeinsam < gemeinsamZiel ? 1 : 0;
        double gruppen = (3.0 * l.GruppenFehlen) + (0.5 * l.GruppenFrueh);
        double gleich = kmJeMannschaft > 0 ? Math.Max(0, l.KmSpanne - spanneMin) / (0.25 * kmJeMannschaft) : 0;
        double pflicht = (10.0 * l.Verstoesse) + (15.0 * l.OhneHeimspieltag) + (3.0 * l.Dreier);
        return km + stand + gemeinsam + gruppen + gleich + pflicht;
    }

    /// <summary>Vergleich nach der Wertung (für das Verlegen der Spieltage).</summary>
    /// <param name="kmMin">Kilometer des kürzesten Plans.</param>
    /// <param name="gemeinsamZiel">Ziel für den gemeinsamen Spieltag.</param>
    /// <param name="spanneMin">Kleinster Unterschied der Kilometer zwischen den Mannschaften.</param>
    /// <param name="kmJeMannschaft">Mittlere Kilometer einer Mannschaft.</param>
    /// <returns>Der Vergleich.</returns>
    public static Comparison<Kompaktloesung> Vergleich(double kmMin, int gemeinsamZiel, double spanneMin, double kmJeMannschaft) =>
        (a, b) => Wertung(a, kmMin, gemeinsamZiel, spanneMin, kmJeMannschaft).CompareTo(Wertung(b, kmMin, gemeinsamZiel, spanneMin, kmJeMannschaft));
}
