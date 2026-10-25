// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace AndiGenerator.Cli;

/// <summary>
/// Hält Windows wach, solange die Kommandozeile rechnet: Ohne das ging ein Laptop bei langen Vergleichen in den
/// Energiesparmodus, und die Läufe danach maßen Schlafenszeit statt Rechenzeit. Ändert keine Einstellung – die Bitte gilt nur,
/// solange das Programm läuft (der Bildschirm darf ausgehen).
/// </summary>
internal static class Wachhalten
{
    /// <summary>Die Bitte gilt fortlaufend (bis zum Ende des Threads bzw. Programms).</summary>
    private const uint FortlaufendFlag = 0x80000000;

    /// <summary>Das System soll nicht in den Energiesparmodus gehen.</summary>
    private const uint SystemFlag = 0x00000001;

    /// <summary>Bittet Windows, nicht in den Energiesparmodus zu gehen (aus dem Haupt-Thread aufrufen); sonst nichts.</summary>
    public static void Einschalten()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = SetThreadExecutionState(FortlaufendFlag | SystemFlag);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
