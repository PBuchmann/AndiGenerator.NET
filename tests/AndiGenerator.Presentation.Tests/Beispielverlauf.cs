// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Ein erfundener Lauf im Automodus für den Verlauf: Basisoptimierung bis 60 s, dann A aus HiGHS bei der Grenze, B aus HiGHS
/// über der Grenze, danach glätten Inseln und Teilneuplanung C; nach 300 s legt der Staffelleiter eine B-Variante fest.
/// </summary>
public static class Beispielverlauf
{
    /// <summary>Erzeugt den Verlauf (ein Punkt je zwei Sekunden, zehn Minuten).</summary>
    /// <returns>Der Verlauf.</returns>
    public static Suchverlauf Erzeugen()
    {
        (double T, int A, int B, int C, string Von)[] schritte =
        [
            (8, 55, 56, 418, "Insel 1"), (15, 49, 52, 402, "Insel 2"), (22, 44, 49, 389, "Insel 3"), (31, 41, 46, 371, "Insel 1"),
            (44, 38, 44, 360, "Insel 4"), (58, 35, 43, 356, "Insel 2"), (78, 10, 29, 392, "HiGHS"), (104, 10, 14, 385, "HiGHS"),
            (140, 10, 14, 377, "Teilneuplanung"), (171, 10, 14, 369, "Insel 3"), (212, 10, 13, 362, "Teilneuplanung"),
            (250, 10, 13, 357, "Insel 1"), (300, 10, 13, 351, "Teilneuplanung"), (352, 10, 13, 347, "Insel 2"),
            (410, 10, 13, 344, "Teilneuplanung"), (430, 10, 12, 343, "Insel 4"), (470, 10, 12, 341, "Insel 1"), (540, 10, 12, 339, "Teilneuplanung"),
        ];
        var verlauf = new Suchverlauf();
        (int a, int b, int c, string von, int nummer) = (62, 61, 431, "Insel 1", 0);
        for (int t = 0; t <= 600; t += 2)
        {
            foreach ((double T, int A, int B, int C, string Von) s in schritte.Where(s => s.T > t - 2 && s.T <= t))
            {
                (a, b, c, von) = (s.A, s.B, s.C, s.Von);
                nummer++;
            }

            bool basis = t < 60;
            string phase = t < 104 ? "Optimierung Stufe A" : "Optimierung Stufe C";
            double highs = 0;
            if (t is >= 62 and <= 104)
            {
                highs = 0.55;
            }
            else if (t is > 104 and < 330)
            {
                highs = 0.08;
            }

            var bester = new Stufenwert(0, a, b, 0, c, c * 1000.0);
            var lenkung = new Lenkungsstand(
                basis ? null : new Stufenwert(0, 35, 43, 0, 356, 356_000),
                bester,
                null,
                string.Empty,
                basis ? "Basisoptimierung" : phase,
                t / 40,
                0,
                Berechnungsoptionen.Standard,
                [],
                SchrankeA: t >= 72 ? 10 : null,
                SchrankeB: t >= 100 ? 11 : null,
                ZuletztVon: von,
                Festgelegt: t >= 300 ? "Plan B optimal" : null);
            Kriterienwert[] kriterien =
            [
                new(Kostenkriterium.ParalleleSpiele, a, Stufe.A),
                new(Kostenkriterium.Sperrtermine, b, Stufe.B),
                new(Kostenkriterium.DreiTageAbstand, c, Stufe.C),
            ];
            var raum = new Maschinenraum([], [], 0, null, 0, kriterien, nummer);
            var last = new Prozessorlast(0.88 - highs, highs, 0.04, 12);
            var stand = new Optimierungsstand(true, false, t * 14_000L, 14_000, c * 1000.0, nummer, TimeSpan.Zero, null, [], lenkung, raum, null, last);
            verlauf.Hinzufuegen(stand, t);
        }

        return verlauf;
    }
}
