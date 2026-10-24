// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Die Verstöße des Plans, wie sie die Bewertung zählt, damit die Diagramme dieselben Zahlen zeigen.</summary>
/// <param name="JeMannschaft">Je Mannschaftsname ihre Verstöße.</param>
/// <param name="ZuLangeSpieltage">Die zu langen Spieltage (0 = erster); leer, wenn nicht berücksichtigt.</param>
/// <param name="UeberlappendeSpieltage">Die überlappenden Spieltage (0 = erster); leer, wenn nicht berücksichtigt.</param>
public sealed record Diagrammverstoesse(
    IReadOnlyDictionary<string, Mannschaftsverstoesse> JeMannschaft,
    IReadOnlySet<int> ZuLangeSpieltage,
    IReadOnlySet<int> UeberlappendeSpieltage)
{
    /// <summary>Holt die Verstöße einer Mannschaft.</summary>
    /// <param name="name">Der Name.</param>
    /// <returns>Die Verstöße oder <c>null</c>.</returns>
    public Mannschaftsverstoesse? Von(string name) => JeMannschaft.GetValueOrDefault(name);
}
