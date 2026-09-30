// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Application;

/// <summary>
/// Namen und Kurzanzeige der Gewichtungen wie in der Kostenansicht des Originals (<c>cCalculateOptionsNamen</c>,
/// <c>cCalculateOptionsDisplayInteger</c>, <c>GetMannschaftKostenDisplayInteger</c>).
/// </summary>
public static class Gewichtungsanzeige
{
    /// <summary>Wert für „nicht berücksichtigen“ (Original −1000, angezeigt als „X“).</summary>
    public const int Ignoriert = -1000;

    private static readonly string[] Stufen = ["Nicht berücksichtigen", "sehr wenig", "wenig", "normal", "hoch", "sehr hoch", "extrem hoch"];

    private static readonly int[] Werte = [Ignoriert, -2, -1, 0, 1, 2, 3];

    private static readonly string[] PlanKostenartNamen =
    [
        "Überlappung Spieltage", "Länge Spieltage", "Überlappung letzter Spieltag", "Länge letzter Spieltag", "Vereinsinterne Spiele am Anfang",
    ];

    /// <summary>Holt alle Gewichtungsstufen in der Reihenfolge des Originals.</summary>
    public static IReadOnlyList<Gewichtung> AlleStufen { get; } = Enum.GetValues<Gewichtung>();

    /// <summary>Name einer Gewichtungsstufe.</summary>
    /// <param name="wert">Die Stufe.</param>
    /// <returns>Z. B. <c>sehr hoch</c>.</returns>
    public static string Name(Gewichtung wert) => Stufen[(int)wert];

    /// <summary>Name einer Plan-Kostenart.</summary>
    /// <param name="art">Die Kostenart.</param>
    /// <returns>Z. B. <c>Länge Spieltage</c>.</returns>
    public static string Name(PlanKostenart art) => PlanKostenartNamen[(int)art];

    /// <summary>Name einer Mannschafts-Kostenart wie in der Kostentabelle.</summary>
    /// <param name="art">Die Kostenart.</param>
    /// <returns>Z. B. <c>Sperrtermine</c>.</returns>
    public static string Name(MannschaftsKostenart art) => Referenzbewertung.Kostenartnamen[(int)art];

    /// <summary>Summiert Gewichtungen wie das Original (<c>Add</c>): „nicht berücksichtigen“ überdeckt alles andere.</summary>
    /// <param name="stufen">Die Gewichtungen, z. B. Kostenart, Mannschaft und Mannschaft je Kostenart.</param>
    /// <returns>Der Anzeigewert, <see cref="Ignoriert"/> für „nicht berücksichtigen“.</returns>
    public static int Wert(params Gewichtung[] stufen)
    {
        ArgumentNullException.ThrowIfNull(stufen);
        int ergebnis = 0;
        foreach (Gewichtung stufe in stufen)
        {
            ergebnis = ergebnis <= Ignoriert || stufe == Gewichtung.NichtBeruecksichtigen ? Ignoriert : ergebnis + Werte[(int)stufe];
        }

        return ergebnis;
    }

    /// <summary>Kurzanzeige neben einem Kostenwert.</summary>
    /// <param name="wert">Anzeigewert aus <see cref="Wert"/>.</param>
    /// <returns><c>X</c>, <c>-2</c>, <c>+1</c> oder leer bei „normal“.</returns>
    public static string Markierung(int wert) => wert switch
    {
        <= Ignoriert => "X",
        < 0 => wert.ToString(CultureInfo.InvariantCulture),
        > 0 => "+" + wert.ToString(CultureInfo.InvariantCulture),
        _ => string.Empty,
    };
}
