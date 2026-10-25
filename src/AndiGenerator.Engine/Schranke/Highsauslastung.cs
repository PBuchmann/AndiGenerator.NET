// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Schranke;

/// <summary>
/// Wie viele HiGHS-Rechnungen gerade laufen (untere Schranke, Alternativen, Teilneuplanung; je Rechnung ein Kern). Der
/// Automodus lässt dafür ebenso viele Rechen-Threads der Inseln ruhen (Vorrang für HiGHS).
/// </summary>
public static class Highsauslastung
{
    private static int laufend;

    /// <summary>Holt die Zahl der laufenden HiGHS-Rechnungen.</summary>
    public static int Laufend => Volatile.Read(ref laufend);

    /// <summary>Eine Rechnung beginnt.</summary>
    internal static void Beginnt() => Interlocked.Increment(ref laufend);

    /// <summary>Eine Rechnung endet.</summary>
    internal static void Endet() => Interlocked.Decrement(ref laufend);
}
