// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Cli;

/// <summary>Wert der Option <c>--rundenplanung</c>.</summary>
internal static class Rundenwahl
{
    public const string Werte = "beide | halbrunde | vorrunde | rueckrunde";

    private static readonly Dictionary<string, Rundenplanung> Namen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["beide"] = Rundenplanung.Beide,
        ["halbrunde"] = Rundenplanung.Halbrunde,
        ["vorrunde"] = Rundenplanung.NurVorrunde,
        ["rueckrunde"] = Rundenplanung.NurRueckrunde,
        ["rückrunde"] = Rundenplanung.NurRueckrunde,
    };

    /// <summary>Liest den Wert; <c>null</c>, wenn er unbekannt ist.</summary>
    public static Rundenplanung? Lesen(string text) => Namen.TryGetValue(text.Trim(), out Rundenplanung wert) ? wert : null;
}
