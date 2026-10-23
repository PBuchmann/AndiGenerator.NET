// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace AndiGenerator.UI;

/// <summary>
/// Bittet Windows, nicht in den Energiesparmodus zu gehen, solange eine Generierung rechnet (der Bildschirm darf ausgehen).
/// Ändert keine Einstellung: Die Bitte gilt nur für den UI-Thread, bis sie zurückgenommen wird oder das Programm endet.
/// </summary>
internal static class Energiesparen
{
    /// <summary>Die Bitte gilt fortlaufend (bis zum nächsten Aufruf).</summary>
    private const uint FortlaufendFlag = 0x80000000;

    /// <summary>Das System soll nicht in den Energiesparmodus gehen.</summary>
    private const uint SystemFlag = 0x00000001;

    /// <summary>Hält den Rechner wach oder gibt den Energiesparmodus wieder frei (auf dem UI-Thread aufrufen).</summary>
    /// <param name="wach"><c>true</c> = wach halten.</param>
    public static void Setzen(bool wach)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = SetThreadExecutionState(wach ? FortlaufendFlag | SystemFlag : FortlaufendFlag);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
