// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Wählt aus den besten Plänen die Varianten: ausgewogen, kürzeste Fahrten, gleichmäßiger Stand und gemeinsamer
/// Spieltag. Fällt eine Wahl auf denselben Plan, wird er einmal mit beiden Namen gezeigt.
/// </summary>
internal static class Kompaktauswahl
{
    public const string Ausgewogen = "Ausgewogen";
    public const string KuerzesteFahrten = "Kürzeste Fahrten";
    public const string GleichmaessigerStand = "Gleichmäßiger Stand";
    public const string GemeinsamerSpieltag = "Gemeinsamer Spieltag";

    /// <summary>Die Varianten, die ausgewogene zuerst.</summary>
    public static List<Kompaktvariante> Varianten(IReadOnlyList<Kompaktloesung> front)
    {
        if (front.Count == 0)
        {
            return [];
        }

        double kmMin = front.Min(l => l.Km);
        int abstandMin = front.Min(l => l.Abstand);
        int gemeinsamMax = front.Max(l => l.Gemeinsam);
        var wahl = new List<(string Name, Kompaktloesung Loesung)>
        {
            (Ausgewogen, front.OrderBy(l => Abweichung(l, kmMin, abstandMin, gemeinsamMax)).ThenBy(l => l.Km).First()),
            (KuerzesteFahrten, front.OrderBy(l => l.Km).ThenBy(l => l.Abstand).ThenByDescending(l => l.Gemeinsam).First()),
            (GleichmaessigerStand, front.OrderBy(l => l.Abstand).ThenBy(l => l.SummeAbstand).ThenBy(l => l.Km).First()),
            (GemeinsamerSpieltag, front.Where(l => l.Gemeinsam == gemeinsamMax).OrderBy(l => l.Km).ThenBy(l => l.Abstand).First()),
        };
        return wahl
            .GroupBy(w => w.Loesung)
            .Select(g => new Kompaktvariante(string.Join(" · ", g.Select(w => w.Name)), g.Key.Planturniere(), g.Key.Kennzahlen()))
            .ToList();
    }

    /// <summary>
    /// Abweichung vom jeweils Besten: 10 % mehr Kilometer wiegen so viel wie ein Spieltag Rückstand im Stand (2 Spiele)
    /// oder wie ein fehlender gemeinsamer Spieltag.
    /// </summary>
    private static double Abweichung(Kompaktloesung l, double kmMin, int abstandMin, int gemeinsamMax)
    {
        double km = kmMin > 0 ? ((l.Km / kmMin) - 1) / 0.1 : 0;
        return km + ((l.Abstand - abstandMin) / 2.0) + (l.Gemeinsam < gemeinsamMax ? 1 : 0);
    }
}
