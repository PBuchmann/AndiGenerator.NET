// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Ob HiGHS auf dieser Plattform mitgeliefert wird (Highs.Native 1.15: nicht für win-arm64).</summary>
internal static class HighsPlattform
{
    /// <summary>Holt den Grund zum Überspringen oder <c>null</c>, wenn HiGHS hier mitgeliefert wird.</summary>
    public static string? Grund =>
        OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "Highs.Native liefert keine Bibliothek für win-arm64; das Programm plant dort ohne HiGHS."
            : null;
}
