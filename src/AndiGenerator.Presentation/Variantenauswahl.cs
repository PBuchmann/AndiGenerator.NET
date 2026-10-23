// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;

namespace AndiGenerator.Presentation;

/// <summary>
/// Welche B-Varianten zur Wahl stehen: nur die mit einer Besonderheit gegenüber dem aktuellen Plan. Eine Variante „Vorrang …“
/// muss in ihrem Kriterium weniger Verstöße haben, der Plan für die Summe in B weniger B-Verstöße (bei höchstens so vielen
/// in A), „A+B“ weniger Verstöße in A und B zusammen. Sonst ist der aktuelle Plan dort schon so gut oder besser, und die
/// Variante zu wählen hieße nur, ihn schlechter zu machen.
/// </summary>
public static class Variantenauswahl
{
    /// <summary>Die Varianten mit einer Besonderheit gegenüber dem aktuellen Plan.</summary>
    /// <param name="varianten">Alle Varianten mit ihrer Bewertung.</param>
    /// <param name="aktuell">Die Bewertung des aktuellen Plans; <c>null</c> = alle Varianten.</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <returns>Die Varianten, die etwas besser machen als der aktuelle Plan.</returns>
    public static IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> Besondere(
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> varianten,
        Planbewertung? aktuell,
        Stufeneinteilung einteilung)
    {
        ArgumentNullException.ThrowIfNull(varianten);
        ArgumentNullException.ThrowIfNull(einteilung);
        if (aktuell is null || varianten.Count == 0)
        {
            return varianten;
        }

        Stufeneinteilung voll = einteilung.Vervollstaendigt();
        IReadOnlyList<Qualitaetskriterium> jetzt = Planqualitaet.Kriterien(aktuell, voll);
        return [.. varianten.Where(v => Besonders(v.Alternative, Planqualitaet.Kriterien(v.Bewertung, voll), jetzt))];
    }

    /// <summary>Ob eine Variante etwas besser macht als der aktuelle Plan (siehe <see cref="Variantenauswahl"/>).</summary>
    private static bool Besonders(Planalternative alternative, IReadOnlyList<Qualitaetskriterium> variante, IReadOnlyList<Qualitaetskriterium> jetzt)
    {
        if (alternative.Kriterium is Kostenkriterium k)
        {
            return Anzahl(variante, k) < Anzahl(jetzt, k);
        }

        (int a, int b) = (Summe(variante, 'A'), Summe(variante, 'B'));
        (int aJetzt, int bJetzt) = (Summe(jetzt, 'A'), Summe(jetzt, 'B'));
        return alternative.AUndB ? a + b < aJetzt + bJetzt : a <= aJetzt && b < bJetzt;
    }

    private static int Anzahl(IReadOnlyList<Qualitaetskriterium> kriterien, Kostenkriterium k) =>
        kriterien.FirstOrDefault(e => e.Kriterium == k)?.Anzahl ?? 0;

    private static int Summe(IReadOnlyList<Qualitaetskriterium> kriterien, char stufe) =>
        kriterien.Where(e => e.Kriterium is not null && e.Stufe.Length > 0 && e.Stufe[0] == stufe).Sum(e => e.Anzahl ?? 0);
}
