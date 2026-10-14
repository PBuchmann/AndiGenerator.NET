// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Test mit dem Löser HiGHS: übersprungen nur unter Windows auf ARM64, weil das Paket Highs.Native dafür keine native
/// Bibliothek mitliefert (das Programm plant dort ohne HiGHS). Überall sonst muss HiGHS laufen.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HighsFactAttribute : FactAttribute
{
    /// <summary>Initialisiert das Attribut.</summary>
    public HighsFactAttribute() => Skip = HighsPlattform.Grund;
}
