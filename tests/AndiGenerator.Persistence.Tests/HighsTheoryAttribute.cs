// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Tests;

/// <summary>Wie <see cref="HighsFactAttribute"/>, für Tests mit Daten.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HighsTheoryAttribute : TheoryAttribute
{
    /// <summary>Initialisiert das Attribut.</summary>
    public HighsTheoryAttribute() => Skip = HighsPlattform.Grund;
}
